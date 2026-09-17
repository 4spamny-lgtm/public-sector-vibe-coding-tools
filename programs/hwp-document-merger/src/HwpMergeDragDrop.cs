using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HancomMergeDragDrop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public sealed class MainForm : Form
    {
        private readonly List<string> _files = new List<string>();
        private bool _running;
        private string _lastOutputPath = "";

        private Panel pnlDrop;
        private Label lblDrop;
        private ListView lvFiles;
        private Label lblCount;

        private Button btnAddFolder;
        private Button btnAddFiles;
        private Button btnRemove;
        private Button btnClear;
        private Button btnUp;
        private Button btnDown;
        private CheckBox chkSubfolders;
        private ComboBox cboOrder;

        private TextBox txtOutputFolder;
        private TextBox txtOutputName;
        private Button btnOutputFolder;

        private CheckBox chkPageBreak;
        private CheckBox chkSourceName;
        private CheckBox chkKeepChar;
        private CheckBox chkKeepPara;
        private CheckBox chkKeepStyle;
        private CheckBox chkKeepSection;
        private CheckBox chkShowHwp;

        private Button btnStart;
        private Button btnOpen;
        private Button btnCheck;
        private ProgressBar progress;
        private Label lblStatus;
        private Label lblCurrent;
        private TextBox txtLog;

        public MainForm()
        {
            Text = "한글 HWP/HWPX 문서 취합 도구";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 720);
            Size = new Size(980, 780);
            Font = new Font("맑은 고딕", 9F);
            Icon = SystemIcons.Application;

            BuildUi();
            WireEvents();
            UpdateCount();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.ColumnCount = 1;
            root.RowCount = 9;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            Controls.Add(root);

            pnlDrop = new Panel();
            pnlDrop.Dock = DockStyle.Fill;
            pnlDrop.BorderStyle = BorderStyle.FixedSingle;
            pnlDrop.BackColor = SystemColors.Window;
            pnlDrop.AllowDrop = true;

            lblDrop = new Label();
            lblDrop.Dock = DockStyle.Fill;
            lblDrop.TextAlign = ContentAlignment.MiddleCenter;
            lblDrop.Text = "HWP / HWPX 파일 또는 폴더를 여기에 끌어놓으세요.\r\n끌어놓은 파일은 아래 목록에 추가됩니다.";
            lblDrop.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            lblDrop.AllowDrop = true;
            pnlDrop.Controls.Add(lblDrop);
            root.Controls.Add(pnlDrop, 0, 0);

            var fileButtons = new FlowLayoutPanel();
            fileButtons.Dock = DockStyle.Fill;
            fileButtons.WrapContents = false;

            btnAddFolder = NewButton("폴더 선택", 92);
            btnAddFiles = NewButton("파일 선택", 92);
            btnRemove = NewButton("선택 제거", 92);
            btnClear = NewButton("전체 비우기", 92);
            btnUp = NewButton("▲ 위로", 78);
            btnDown = NewButton("▼ 아래로", 78);

            chkSubfolders = new CheckBox();
            chkSubfolders.Text = "하위 폴더 포함";
            chkSubfolders.AutoSize = true;
            chkSubfolders.Margin = new Padding(12, 10, 0, 0);

            var orderLabel = new Label();
            orderLabel.Text = "취합 순서:";
            orderLabel.AutoSize = true;
            orderLabel.Margin = new Padding(14, 11, 3, 0);

            cboOrder = new ComboBox();
            cboOrder.DropDownStyle = ComboBoxStyle.DropDownList;
            cboOrder.Items.Add("목록 순서(드래그/추가 순서)");
            cboOrder.Items.Add("파일명 자연정렬(1, 2, 10)");
            cboOrder.SelectedIndex = 0;
            cboOrder.Width = 205;
            cboOrder.Margin = new Padding(0, 6, 0, 0);

            lblCount = new Label();
            lblCount.AutoSize = true;
            lblCount.Margin = new Padding(14, 11, 0, 0);

            fileButtons.Controls.Add(btnAddFolder);
            fileButtons.Controls.Add(btnAddFiles);
            fileButtons.Controls.Add(btnRemove);
            fileButtons.Controls.Add(btnClear);
            fileButtons.Controls.Add(btnUp);
            fileButtons.Controls.Add(btnDown);
            fileButtons.Controls.Add(chkSubfolders);
            fileButtons.Controls.Add(orderLabel);
            fileButtons.Controls.Add(cboOrder);
            fileButtons.Controls.Add(lblCount);
            root.Controls.Add(fileButtons, 0, 1);

            lvFiles = new ListView();
            lvFiles.Dock = DockStyle.Fill;
            lvFiles.View = View.Details;
            lvFiles.FullRowSelect = true;
            lvFiles.GridLines = true;
            lvFiles.MultiSelect = true;
            lvFiles.AllowDrop = true;
            lvFiles.Columns.Add("순서", 58, HorizontalAlignment.Center);
            lvFiles.Columns.Add("취합할 파일", 790);
            root.Controls.Add(lvFiles, 0, 2);

            var output = new GroupBox();
            output.Text = "결과 파일";
            output.Dock = DockStyle.Fill;
            var outTable = new TableLayoutPanel();
            outTable.Dock = DockStyle.Fill;
            outTable.Padding = new Padding(10, 8, 10, 6);
            outTable.ColumnCount = 4;
            outTable.RowCount = 2;
            outTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            outTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            outTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235));
            outTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            outTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

            var lblOutFolder = new Label();
            lblOutFolder.Text = "저장 폴더";
            lblOutFolder.Dock = DockStyle.Fill;
            lblOutFolder.TextAlign = ContentAlignment.MiddleLeft;
            outTable.Controls.Add(lblOutFolder, 0, 0);

            txtOutputFolder = new TextBox();
            txtOutputFolder.Dock = DockStyle.Fill;
            outTable.Controls.Add(txtOutputFolder, 1, 0);

            btnOutputFolder = NewButton("찾아보기", 90);
            outTable.Controls.Add(btnOutputFolder, 2, 0);

            var hint = new Label();
            hint.Text = "첫 파일을 추가하면 자동으로 채워집니다.";
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            outTable.Controls.Add(hint, 3, 0);

            var lblOutName = new Label();
            lblOutName.Text = "파일명";
            lblOutName.Dock = DockStyle.Fill;
            lblOutName.TextAlign = ContentAlignment.MiddleLeft;
            outTable.Controls.Add(lblOutName, 0, 1);

            txtOutputName = new TextBox();
            txtOutputName.Text = "취합.hwpx";
            txtOutputName.Dock = DockStyle.Fill;
            outTable.Controls.Add(txtOutputName, 1, 1);

            var hint2 = new Label();
            hint2.Text = "결과는 HWPX로 저장됩니다.";
            hint2.Dock = DockStyle.Fill;
            hint2.TextAlign = ContentAlignment.MiddleLeft;
            outTable.Controls.Add(hint2, 3, 1);

            output.Controls.Add(outTable);
            root.Controls.Add(output, 0, 3);

            var options = new GroupBox();
            options.Text = "취합 옵션";
            options.Dock = DockStyle.Fill;

            var optFlow = new FlowLayoutPanel();
            optFlow.Dock = DockStyle.Fill;
            optFlow.Padding = new Padding(10, 8, 10, 6);
            optFlow.WrapContents = true;

            chkPageBreak = NewCheck("파일 사이 쪽 나누기", true, 150);
            chkSourceName = NewCheck("원본 파일명 표시", true, 145);
            chkKeepChar = NewCheck("글자모양 유지", true, 125);
            chkKeepPara = NewCheck("문단모양 유지", true, 125);
            chkKeepStyle = NewCheck("스타일 유지", true, 110);
            chkKeepSection = NewCheck("쪽 모양 유지", false, 115);
            chkShowHwp = NewCheck("작업 중 한글 창 표시", true, 160);

            optFlow.Controls.Add(chkPageBreak);
            optFlow.Controls.Add(chkSourceName);
            optFlow.Controls.Add(chkKeepChar);
            optFlow.Controls.Add(chkKeepPara);
            optFlow.Controls.Add(chkKeepStyle);
            optFlow.Controls.Add(chkKeepSection);
            optFlow.Controls.Add(chkShowHwp);

            options.Controls.Add(optFlow);
            root.Controls.Add(options, 0, 4);

            var actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.WrapContents = false;

            btnStart = NewButton("취합 시작", 140);
            btnStart.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            btnOpen = NewButton("완료 파일 열기", 125);
            btnOpen.Enabled = false;
            btnCheck = NewButton("환경 점검", 100);

            actions.Controls.Add(btnStart);
            actions.Controls.Add(btnOpen);
            actions.Controls.Add(new Label { Width = 15 });
            actions.Controls.Add(btnCheck);
            root.Controls.Add(actions, 0, 5);

            lblStatus = new Label();
            lblStatus.Text = "파일을 추가하세요.";
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            root.Controls.Add(lblStatus, 0, 6);

            progress = new ProgressBar();
            progress.Dock = DockStyle.Fill;
            progress.Minimum = 0;
            progress.Maximum = 100;
            root.Controls.Add(progress, 0, 7);

            txtLog = new TextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.BackColor = SystemColors.Window;
            root.Controls.Add(txtLog, 0, 8);

            lblCurrent = new Label();
            lblCurrent.Visible = false;
            Controls.Add(lblCurrent);
        }

        private Button NewButton(string text, int width)
        {
            var b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.Margin = new Padding(3, 5, 3, 3);
            return b;
        }

        private CheckBox NewCheck(string text, bool value, int width)
        {
            var c = new CheckBox();
            c.Text = text;
            c.Checked = value;
            c.Width = width;
            c.Margin = new Padding(5, 8, 5, 5);
            return c;
        }

        private void WireEvents()
        {
            btnAddFolder.Click += delegate { AddFolderDialog(); };
            btnAddFiles.Click += delegate { AddFilesDialog(); };
            btnRemove.Click += delegate { RemoveSelected(); };
            btnClear.Click += delegate { ClearFiles(); };
            btnUp.Click += delegate { MoveSelected(-1); };
            btnDown.Click += delegate { MoveSelected(1); };
            btnOutputFolder.Click += delegate { ChooseOutputFolder(); };
            btnStart.Click += delegate { StartMerge(); };
            btnOpen.Click += delegate { OpenResult(); };
            btnCheck.Click += delegate { CheckEnvironment(); };

            pnlDrop.DragEnter += DragEnterHandler;
            pnlDrop.DragDrop += DragDropHandler;
            lblDrop.DragEnter += DragEnterHandler;
            lblDrop.DragDrop += DragDropHandler;
            lvFiles.DragEnter += DragEnterHandler;
            lvFiles.DragDrop += DragDropHandler;

            lvFiles.Resize += delegate
            {
                if (lvFiles.Columns.Count >= 2)
                    lvFiles.Columns[1].Width = Math.Max(300, lvFiles.ClientSize.Width - lvFiles.Columns[0].Width - 5);
            };

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_running)
                {
                    MessageBox.Show("취합 작업 중에는 프로그램을 닫을 수 없습니다.",
                        "작업 중", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    e.Cancel = true;
                }
            };
        }

        private void DragEnterHandler(object sender, DragEventArgs e)
        {
            if (!_running && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void DragDropHandler(object sender, DragEventArgs e)
        {
            if (_running || e.Data == null) return;
            var items = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (items == null) return;
            AddPaths(items);
        }

        private void AddFolderDialog()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "취합할 HWP/HWPX 파일이 있는 폴더를 선택하세요.";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddPaths(new[] { dlg.SelectedPath });
            }
        }

        private void AddFilesDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "취합할 한글 문서 선택";
                dlg.Filter = "한글 문서 (*.hwp;*.hwpx)|*.hwp;*.hwpx|HWPX (*.hwpx)|*.hwpx|HWP (*.hwp)|*.hwp";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddPaths(dlg.FileNames);
            }
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            int before = _files.Count;

            foreach (string path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        AddOneFile(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        var list = new List<string>();
                        CollectFolderFiles(path, list, chkSubfolders.Checked);
                        list.Sort(NaturalPathComparer.Instance);
                        foreach (string f in list)
                            AddOneFile(f);
                    }
                }
                catch (Exception ex)
                {
                    Log("추가 실패: " + path + " / " + ex.Message);
                }
            }

            RefreshList();

            if (_files.Count > 0 && string.IsNullOrWhiteSpace(txtOutputFolder.Text))
                txtOutputFolder.Text = Path.GetDirectoryName(_files[0]);

            Log((_files.Count - before) + "개 파일을 추가했습니다.");
        }

        private static void CollectFolderFiles(string folder, List<string> result, bool recursive)
        {
            try
            {
                foreach (string f in Directory.GetFiles(folder))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".hwp" || ext == ".hwpx")
                        result.Add(f);
                }

                if (!recursive) return;

                foreach (string sub in Directory.GetDirectories(folder))
                {
                    try { CollectFolderFiles(sub, result, true); }
                    catch { }
                }
            }
            catch { }
        }

        private void AddOneFile(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".hwp" && ext != ".hwpx") return;

            string full = Path.GetFullPath(path);
            foreach (string existing in _files)
            {
                if (string.Equals(existing, full, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            _files.Add(full);
        }

        private void RefreshList()
        {
            lvFiles.BeginUpdate();
            lvFiles.Items.Clear();

            for (int i = 0; i < _files.Count; i++)
            {
                var item = new ListViewItem((i + 1).ToString());
                item.SubItems.Add(_files[i]);
                item.ToolTipText = _files[i];
                lvFiles.Items.Add(item);
            }

            lvFiles.EndUpdate();
            UpdateCount();
        }

        private void UpdateCount()
        {
            lblCount.Text = "총 " + _files.Count + "개";
        }

        private void RemoveSelected()
        {
            if (lvFiles.SelectedIndices.Count == 0) return;

            var indices = new List<int>();
            foreach (int i in lvFiles.SelectedIndices) indices.Add(i);
            indices.Sort();
            indices.Reverse();

            foreach (int i in indices)
                if (i >= 0 && i < _files.Count) _files.RemoveAt(i);

            RefreshList();
        }

        private void ClearFiles()
        {
            _files.Clear();
            RefreshList();
            progress.Value = 0;
            lblStatus.Text = "파일을 추가하세요.";
            txtLog.Clear();
            btnOpen.Enabled = false;
            _lastOutputPath = "";
        }

        private void MoveSelected(int direction)
        {
            if (lvFiles.SelectedIndices.Count != 1) return;
            int i = lvFiles.SelectedIndices[0];
            int j = i + direction;
            if (j < 0 || j >= _files.Count) return;

            string tmp = _files[i];
            _files[i] = _files[j];
            _files[j] = tmp;
            RefreshList();
            lvFiles.Items[j].Selected = true;
            lvFiles.Items[j].Focused = true;
            lvFiles.EnsureVisible(j);
        }

        private void ChooseOutputFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "취합 결과를 저장할 폴더를 선택하세요.";
                if (Directory.Exists(txtOutputFolder.Text))
                    dlg.SelectedPath = txtOutputFolder.Text;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtOutputFolder.Text = dlg.SelectedPath;
            }
        }

        private void StartMerge()
        {
            if (_running) return;

            if (_files.Count == 0)
            {
                MessageBox.Show("취합할 HWP/HWPX 파일을 먼저 추가하세요.",
                    "파일 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outFolder = txtOutputFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(outFolder))
            {
                MessageBox.Show("결과 저장 폴더를 선택하세요.",
                    "저장 폴더", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try { Directory.CreateDirectory(outFolder); }
            catch (Exception ex)
            {
                MessageBox.Show("저장 폴더를 사용할 수 없습니다.\r\n" + ex.Message,
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string outputName = txtOutputName.Text.Trim();
            if (outputName.Length == 0) outputName = "취합.hwpx";
            if (!outputName.EndsWith(".hwpx", StringComparison.OrdinalIgnoreCase))
                outputName += ".hwpx";

            foreach (char ch in Path.GetInvalidFileNameChars())
            {
                if (outputName.IndexOf(ch) >= 0)
                {
                    MessageBox.Show("결과 파일명에 사용할 수 없는 문자가 있습니다.",
                        "파일명 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            string outputPath = Path.Combine(outFolder, outputName);

            var sources = new List<string>(_files);
            sources.RemoveAll(delegate(string x)
            {
                try { return string.Equals(Path.GetFullPath(x), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });

            if (sources.Count == 0)
            {
                MessageBox.Show("취합할 원본 파일이 없습니다.", "확인",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboOrder.SelectedIndex == 1)
                sources.Sort(NaturalPathComparer.Instance);

            if (File.Exists(outputPath))
            {
                if (MessageBox.Show("같은 이름의 결과 파일이 이미 있습니다.\r\n덮어쓸까요?\r\n\r\n" + outputPath,
                    "덮어쓰기 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            var opt = new MergeOptions();
            opt.Sources = sources;
            opt.OutputPath = outputPath;
            opt.PageBreak = chkPageBreak.Checked;
            opt.SourceName = chkSourceName.Checked;
            opt.KeepChar = chkKeepChar.Checked;
            opt.KeepPara = chkKeepPara.Checked;
            opt.KeepStyle = chkKeepStyle.Checked;
            opt.KeepSection = chkKeepSection.Checked;
            opt.ShowHwp = chkShowHwp.Checked;

            _running = true;
            _lastOutputPath = "";
            btnOpen.Enabled = false;
            SetUiEnabled(false);
            progress.Value = 0;
            txtLog.Clear();
            lblStatus.Text = "준비 중...";

            var worker = new Thread(new ThreadStart(delegate
            {
                MergeWorker(opt);
            }));
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private void MergeWorker(MergeOptions opt)
        {
            MergeResult result = HwpAutomation.Merge(
                opt,
                delegate(int percent, string status, string current)
                {
                    Ui(delegate
                    {
                        progress.Value = Math.Max(0, Math.Min(100, percent));
                        lblStatus.Text = status + (string.IsNullOrWhiteSpace(current) ? "" : " : " + current);
                    });
                },
                delegate(string message)
                {
                    Ui(delegate { Log(message); });
                }
            );

            Ui(delegate
            {
                _running = false;
                SetUiEnabled(true);

                if (result.Saved && File.Exists(result.OutputPath))
                {
                    _lastOutputPath = result.OutputPath;
                    btnOpen.Enabled = true;
                    progress.Value = 100;
                }

                lblStatus.Text = result.Saved
                    ? "완료 - 성공 " + result.SuccessCount + "개 / 실패 " + result.FailCount + "개"
                    : "취합 실패";

                string message = result.Message;
                if (!string.IsNullOrWhiteSpace(result.LogPath))
                    message += "\r\n\r\n오류 로그: " + result.LogPath;

                MessageBox.Show(this, message,
                    result.Saved ? "취합 완료" : "취합 실패",
                    MessageBoxButtons.OK,
                    result.Saved ? (result.FailCount == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning)
                                 : MessageBoxIcon.Error);
            });
        }

        private void OpenResult()
        {
            if (string.IsNullOrWhiteSpace(_lastOutputPath) || !File.Exists(_lastOutputPath))
            {
                MessageBox.Show("완료 파일을 찾을 수 없습니다.",
                    "확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(_lastOutputPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 열 수 없습니다.\r\n" + ex.Message,
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckEnvironment()
        {
            string report;
            bool ok = HwpAutomation.CheckEnvironment(out report);
            MessageBox.Show(this, report,
                ok ? "환경 점검 완료" : "환경 점검",
                MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        private void SetUiEnabled(bool enabled)
        {
            btnAddFolder.Enabled = enabled;
            btnAddFiles.Enabled = enabled;
            btnRemove.Enabled = enabled;
            btnClear.Enabled = enabled;
            btnUp.Enabled = enabled;
            btnDown.Enabled = enabled;
            chkSubfolders.Enabled = enabled;
            cboOrder.Enabled = enabled;
            txtOutputFolder.Enabled = enabled;
            txtOutputName.Enabled = enabled;
            btnOutputFolder.Enabled = enabled;
            chkPageBreak.Enabled = enabled;
            chkSourceName.Enabled = enabled;
            chkKeepChar.Enabled = enabled;
            chkKeepPara.Enabled = enabled;
            chkKeepStyle.Enabled = enabled;
            chkKeepSection.Enabled = enabled;
            chkShowHwp.Enabled = enabled;
            btnStart.Enabled = enabled;
            btnCheck.Enabled = enabled;
            pnlDrop.AllowDrop = enabled;
            lblDrop.AllowDrop = enabled;
            lvFiles.AllowDrop = enabled;
        }

        private void Log(string message)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }

        private void Ui(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }
    }

    internal sealed class MergeOptions
    {
        public List<string> Sources = new List<string>();
        public string OutputPath = "";
        public bool PageBreak;
        public bool SourceName;
        public bool KeepChar;
        public bool KeepPara;
        public bool KeepStyle;
        public bool KeepSection;
        public bool ShowHwp;
    }

    internal sealed class MergeResult
    {
        public int SuccessCount;
        public int FailCount;
        public string OutputPath = "";
        public string LogPath = "";
        public string Message = "";
        public bool Saved;
    }

    internal static class HwpAutomation
    {
        public static bool CheckEnvironment(out string report)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("HWPFrame.HwpObject", false);
                if (t == null)
                {
                    report = "한글 자동화 객체(HWPFrame.HwpObject)를 찾지 못했습니다.\r\n한컴오피스 한글 설치 상태를 확인하세요.";
                    return false;
                }

                List<string> modules = GetSecurityModuleNames();
                if (modules.Count == 0)
                {
                    report = "한글 자동화는 사용 가능합니다.\r\n다만 등록된 FilePathCheckDLL 보안 모듈을 찾지 못했습니다.\r\n취합 중 파일 접근 승인창이 나타날 수 있습니다.";
                    return true;
                }

                report = "한글 자동화 사용 가능.\r\n보안 모듈 등록 후보: " + string.Join(", ", modules.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                report = "환경 점검 중 오류: " + DeepMessage(ex);
                return false;
            }
        }

        public static MergeResult Merge(
            MergeOptions opt,
            Action<int, string, string> progress,
            Action<string> log)
        {
            var result = new MergeResult();
            result.OutputPath = opt.OutputPath;
            dynamic hwp = null;
            var errors = new List<string>();

            try
            {
                if (opt.Sources == null || opt.Sources.Count == 0)
                {
                    result.Message = "취합할 파일이 없습니다.";
                    return result;
                }

                progress(0, "한글을 실행하는 중...", "");

                Type hwpType = Type.GetTypeFromProgID("HWPFrame.HwpObject", true);
                hwp = Activator.CreateInstance(hwpType);

                bool securityRegistered = TryRegisterSecurityModule(hwp);
                if (!securityRegistered)
                    log("보안모듈이 등록되지 않았습니다. 한글 파일 접근 승인창이 나타날 수 있습니다.");

                try { hwp.XHwpWindows.Item(0).Visible = opt.ShowHwp; }
                catch { }

                int inserted = 0;

                for (int i = 0; i < opt.Sources.Count; i++)
                {
                    string filePath = opt.Sources[i];
                    string fileName = Path.GetFileName(filePath);
                    int pct = (int)Math.Floor((double)i / opt.Sources.Count * 96.0);

                    progress(pct, "취합 중 " + (i + 1) + " / " + opt.Sources.Count, fileName);

                    try
                    {
                        if (!File.Exists(filePath))
                            throw new FileNotFoundException("파일을 찾을 수 없습니다.", filePath);

                        if (inserted > 0 && opt.PageBreak)
                            hwp.Run("BreakPage");

                        if (opt.SourceName)
                        {
                            dynamic insertText = hwp.HParameterSet.HInsertText;
                            hwp.HAction.GetDefault("InsertText", insertText.HSet);
                            insertText.Text = "[출처: " + fileName + "]\r";
                            hwp.HAction.Execute("InsertText", insertText.HSet);
                        }

                        dynamic insertFile = hwp.HParameterSet.HInsertFile;
                        hwp.HAction.GetDefault("InsertFile", insertFile.HSet);
                        insertFile.Filename = filePath;
                        insertFile.KeepSection = opt.KeepSection ? 1 : 0;
                        insertFile.KeepCharshape = opt.KeepChar ? 1 : 0;
                        insertFile.KeepParashape = opt.KeepPara ? 1 : 0;
                        insertFile.KeepStyle = opt.KeepStyle ? 1 : 0;
                        hwp.HAction.Execute("InsertFile", insertFile.HSet);
                        hwp.Run("MoveDocEnd");

                        inserted++;
                        result.SuccessCount++;
                        log("[완료] " + fileName);
                    }
                    catch (Exception fileEx)
                    {
                        result.FailCount++;
                        string err = fileName + " | " + DeepMessage(fileEx);
                        errors.Add(err);
                        log("[실패] " + err);
                    }
                }

                if (result.SuccessCount == 0)
                {
                    result.Message = "취합에 성공한 파일이 없습니다.";
                    return result;
                }

                progress(97, "결과 파일 저장 중...", opt.OutputPath);

                if (File.Exists(opt.OutputPath))
                    File.Delete(opt.OutputPath);

                object saveResult = hwp.SaveAs(opt.OutputPath, "HWPX", "");
                result.Saved = File.Exists(opt.OutputPath);

                if (!result.Saved)
                    throw new IOException("저장 후 결과 HWPX 파일을 확인할 수 없습니다.");

                progress(100, "완료", opt.OutputPath);

                if (errors.Count > 0)
                {
                    result.LogPath = WriteLog(opt, errors, securityRegistered);
                    result.Message = "총 " + result.SuccessCount + "개 파일 취합 완료\r\n" +
                                     result.FailCount + "개 파일은 오류로 건너뛰었습니다.\r\n\r\n" +
                                     opt.OutputPath;
                }
                else
                {
                    result.Message = "총 " + result.SuccessCount + "개 파일 취합 완료\r\n\r\n" +
                                     opt.OutputPath;
                }
            }
            catch (Exception ex)
            {
                result.Message = "오류가 발생했습니다.\r\n\r\n" + DeepMessage(ex);
                if (errors.Count > 0)
                    result.LogPath = WriteLog(opt, errors, false);
            }
            finally
            {
                if (hwp != null)
                {
                    try { hwp.Quit(); } catch { }
                    try
                    {
                        if (Marshal.IsComObject(hwp))
                            Marshal.FinalReleaseComObject(hwp);
                    }
                    catch { }
                    hwp = null;
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            return result;
        }

        private static bool TryRegisterSecurityModule(dynamic hwp)
        {
            List<string> names = GetSecurityModuleNames();
            AddUnique(names, "FilePathCheckerModuleExample");
            AddUnique(names, "FilePathCheckerModule");

            foreach (string name in names)
            {
                try
                {
                    object value = hwp.RegisterModule("FilePathCheckDLL", name);
                    if (value is bool && (bool)value) return true;
                    if (value != null && string.Equals(Convert.ToString(value), "True", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { }
            }
            return false;
        }

        private static List<string> GetSecurityModuleNames()
        {
            var names = new List<string>();

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\HNC\HwpAutomation\Modules"))
                {
                    if (key != null)
                    {
                        foreach (string name in key.GetValueNames())
                        {
                            object value = key.GetValue(name);
                            if (!string.IsNullOrWhiteSpace(name) &&
                                value is string &&
                                !string.IsNullOrWhiteSpace((string)value))
                                AddUnique(names, name);
                        }
                    }
                }
            }
            catch { }

            return names;
        }

        private static void AddUnique(List<string> list, string value)
        {
            foreach (string item in list)
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                    return;
            list.Add(value);
        }

        private static string WriteLog(MergeOptions opt, List<string> errors, bool securityRegistered)
        {
            try
            {
                string folder = Path.GetDirectoryName(opt.OutputPath);
                string logPath = Path.Combine(folder, "취합_오류로그_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

                var lines = new List<string>();
                lines.Add("한글 문서 취합 오류 로그");
                lines.Add("일시: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                lines.Add("결과 파일: " + opt.OutputPath);
                lines.Add("보안 모듈 등록 성공: " + (securityRegistered ? "예" : "아니오"));
                lines.Add("");
                lines.AddRange(errors);

                File.WriteAllLines(logPath, lines.ToArray(), System.Text.Encoding.UTF8);
                return logPath;
            }
            catch
            {
                return "";
            }
        }

        private static string DeepMessage(Exception ex)
        {
            if (ex == null) return "알 수 없는 오류";
            string msg = ex.Message;
            Exception cur = ex;
            while (cur.InnerException != null)
            {
                cur = cur.InnerException;
                if (!string.IsNullOrWhiteSpace(cur.Message) && msg.IndexOf(cur.Message, StringComparison.Ordinal) < 0)
                    msg += " | " + cur.Message;
            }
            return msg;
        }
    }

    internal sealed class NaturalPathComparer : IComparer<string>
    {
        public static readonly NaturalPathComparer Instance = new NaturalPathComparer();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string x, string y);

        public int Compare(string x, string y)
        {
            string a = Path.GetFileName(x ?? "");
            string b = Path.GetFileName(y ?? "");
            return StrCmpLogicalW(a, b);
        }
    }
}
