using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HancomPdfBatch
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
        private const string HwpProgId = "HWPFrame.HwpObject";
        private const string SecurityRegPath = @"SOFTWARE\HNC\HwpAutomation\Modules";
        private const string DefaultSecurityName = "FilePathCheckerModuleExample";

        private readonly List<string> _files = new List<string>();
        private volatile bool _cancelRequested;
        private bool _running;

        private Panel pnlDrop;
        private Label lblDrop;
        private ListView lvFiles;
        private Label lblCount;
        private Button btnAddFolder;
        private Button btnAddFiles;
        private Button btnRemove;
        private Button btnClear;

        private CheckBox chkSubfolders;
        private RadioButton rdoSameFolder;
        private RadioButton rdoOutputFolder;
        private TextBox txtOutputFolder;
        private Button btnOutputFolder;
        private ComboBox cboExisting;

        private Button btnStart;
        private Button btnCancel;
        private Button btnOpenOutput;
        private Button btnCheck;
        private Button btnSecurity;

        private ProgressBar progress;
        private Label lblStatus;
        private TextBox txtLog;

        private string _lastOutputFolder = "";

        public MainForm()
        {
            Text = "한글 HWP/HWPX → PDF 일괄 변환";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(840, 650);
            Size = new Size(940, 740);
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
            root.RowCount = 8;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            Controls.Add(root);

            pnlDrop = new Panel();
            pnlDrop.Dock = DockStyle.Fill;
            pnlDrop.BorderStyle = BorderStyle.FixedSingle;
            pnlDrop.BackColor = SystemColors.Window;
            pnlDrop.AllowDrop = true;

            lblDrop = new Label();
            lblDrop.Dock = DockStyle.Fill;
            lblDrop.TextAlign = ContentAlignment.MiddleCenter;
            lblDrop.Text = "HWP / HWPX 파일 또는 폴더를 여기에 끌어놓으세요.\r\n버튼으로 선택해도 됩니다.";
            lblDrop.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            lblDrop.AllowDrop = true;
            pnlDrop.Controls.Add(lblDrop);
            root.Controls.Add(pnlDrop, 0, 0);

            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = false;

            btnAddFolder = NewButton("폴더 선택", 98);
            btnAddFiles = NewButton("파일 선택", 98);
            btnRemove = NewButton("선택 제거", 98);
            btnClear = NewButton("전체 비우기", 98);
            chkSubfolders = new CheckBox();
            chkSubfolders.Text = "하위 폴더 포함";
            chkSubfolders.AutoSize = true;
            chkSubfolders.Margin = new Padding(16, 8, 0, 0);
            lblCount = new Label();
            lblCount.AutoSize = true;
            lblCount.Margin = new Padding(20, 9, 0, 0);

            buttons.Controls.Add(btnAddFolder);
            buttons.Controls.Add(btnAddFiles);
            buttons.Controls.Add(btnRemove);
            buttons.Controls.Add(btnClear);
            buttons.Controls.Add(chkSubfolders);
            buttons.Controls.Add(lblCount);
            root.Controls.Add(buttons, 0, 1);

            lvFiles = new ListView();
            lvFiles.Dock = DockStyle.Fill;
            lvFiles.View = View.Details;
            lvFiles.FullRowSelect = true;
            lvFiles.GridLines = true;
            lvFiles.MultiSelect = true;
            lvFiles.AllowDrop = true;
            lvFiles.Columns.Add("변환할 파일", 760);
            root.Controls.Add(lvFiles, 0, 2);

            var options = new GroupBox();
            options.Dock = DockStyle.Fill;
            options.Text = "PDF 저장 옵션";

            var opt = new TableLayoutPanel();
            opt.Dock = DockStyle.Fill;
            opt.Padding = new Padding(10, 6, 10, 6);
            opt.ColumnCount = 4;
            opt.RowCount = 3;
            opt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175));
            opt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            opt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            opt.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            opt.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            options.Controls.Add(opt);

            rdoSameFolder = new RadioButton();
            rdoSameFolder.Text = "원본 파일과 같은 폴더";
            rdoSameFolder.Checked = true;
            rdoSameFolder.AutoSize = true;
            rdoSameFolder.Margin = new Padding(3, 8, 3, 3);
            opt.Controls.Add(rdoSameFolder, 0, 0);
            opt.SetColumnSpan(rdoSameFolder, 2);

            rdoOutputFolder = new RadioButton();
            rdoOutputFolder.Text = "지정 폴더에 모두 저장";
            rdoOutputFolder.AutoSize = true;
            rdoOutputFolder.Margin = new Padding(3, 8, 3, 3);
            opt.Controls.Add(rdoOutputFolder, 0, 1);

            txtOutputFolder = new TextBox();
            txtOutputFolder.Dock = DockStyle.Fill;
            txtOutputFolder.Enabled = false;
            opt.Controls.Add(txtOutputFolder, 1, 1);

            btnOutputFolder = NewButton("찾아보기", 100);
            btnOutputFolder.Enabled = false;
            opt.Controls.Add(btnOutputFolder, 2, 1);

            var lblExisting = new Label();
            lblExisting.Text = "PDF가 이미 있을 때";
            lblExisting.TextAlign = ContentAlignment.MiddleLeft;
            lblExisting.Dock = DockStyle.Fill;
            opt.Controls.Add(lblExisting, 0, 2);

            cboExisting = new ComboBox();
            cboExisting.DropDownStyle = ComboBoxStyle.DropDownList;
            cboExisting.Items.Add("자동 이름 변경 (권장)");
            cboExisting.Items.Add("기존 PDF 건너뛰기");
            cboExisting.Items.Add("기존 PDF 덮어쓰기");
            cboExisting.SelectedIndex = 0;
            cboExisting.Dock = DockStyle.Fill;
            opt.Controls.Add(cboExisting, 1, 2);
            opt.SetColumnSpan(cboExisting, 2);

            var hint = new Label();
            hint.Text = "※ 출력 파일명은 원본 이름 그대로 .pdf가 됩니다.";
            hint.AutoSize = true;
            hint.Margin = new Padding(8, 9, 0, 0);
            opt.Controls.Add(hint, 3, 2);

            root.Controls.Add(options, 0, 3);

            var actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.LeftToRight;
            actionPanel.WrapContents = false;

            btnStart = NewButton("PDF 변환 시작", 150);
            btnStart.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            btnCancel = NewButton("취소", 90);
            btnCancel.Enabled = false;
            btnOpenOutput = NewButton("결과 폴더 열기", 125);
            btnOpenOutput.Enabled = false;

            var spacer = new Label();
            spacer.Width = 20;

            btnCheck = NewButton("환경 점검", 100);
            btnSecurity = NewButton("보안모듈 설정", 120);

            actionPanel.Controls.Add(btnStart);
            actionPanel.Controls.Add(btnCancel);
            actionPanel.Controls.Add(btnOpenOutput);
            actionPanel.Controls.Add(spacer);
            actionPanel.Controls.Add(btnCheck);
            actionPanel.Controls.Add(btnSecurity);
            root.Controls.Add(actionPanel, 0, 4);

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "파일을 추가하세요.";
            root.Controls.Add(lblStatus, 0, 5);

            progress = new ProgressBar();
            progress.Dock = DockStyle.Fill;
            progress.Minimum = 0;
            progress.Maximum = 100;
            root.Controls.Add(progress, 0, 6);

            txtLog = new TextBox();
            txtLog.Dock = DockStyle.Fill;
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.BackColor = SystemColors.Window;
            root.Controls.Add(txtLog, 0, 7);
        }

        private Button NewButton(string text, int width)
        {
            var b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 29;
            b.Margin = new Padding(3, 4, 3, 3);
            return b;
        }

        private void WireEvents()
        {
            btnAddFolder.Click += delegate { AddFolderByDialog(); };
            btnAddFiles.Click += delegate { AddFilesByDialog(); };
            btnRemove.Click += delegate { RemoveSelected(); };
            btnClear.Click += delegate { ClearFiles(); };
            btnOutputFolder.Click += delegate { ChooseOutputFolder(); };
            btnStart.Click += delegate { StartConversion(); };
            btnCancel.Click += delegate { _cancelRequested = true; Log("취소 요청됨: 현재 파일 처리 후 중지합니다."); };
            btnOpenOutput.Click += delegate { OpenLastOutput(); };
            btnCheck.Click += delegate { CheckEnvironment(); };
            btnSecurity.Click += delegate { SetupSecurityModule(); };

            rdoSameFolder.CheckedChanged += delegate { UpdateOutputControls(); };
            rdoOutputFolder.CheckedChanged += delegate { UpdateOutputControls(); };

            pnlDrop.DragEnter += DragEnterHandler;
            pnlDrop.DragDrop += DragDropHandler;
            lblDrop.DragEnter += DragEnterHandler;
            lblDrop.DragDrop += DragDropHandler;
            lvFiles.DragEnter += DragEnterHandler;
            lvFiles.DragDrop += DragDropHandler;

            lvFiles.Resize += delegate
            {
                if (lvFiles.Columns.Count > 0)
                    lvFiles.Columns[0].Width = Math.Max(200, lvFiles.ClientSize.Width - 5);
            };

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (_running)
                {
                    MessageBox.Show("변환 작업 중에는 프로그램을 닫을 수 없습니다.\r\n먼저 '취소'를 눌러주세요.",
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

        private void AddFolderByDialog()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "HWP/HWPX 파일이 있는 폴더를 선택하세요.";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddPaths(new[] { dlg.SelectedPath });
            }
        }

        private void AddFilesByDialog()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "PDF로 변환할 한글 파일 선택";
                dlg.Filter = "한글 문서 (*.hwp;*.hwpx)|*.hwp;*.hwpx|HWP (*.hwp)|*.hwp|HWPX (*.hwpx)|*.hwpx";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    AddPaths(dlg.FileNames);
            }
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            int before = _files.Count;

            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        AddFileIfSupported(path);
                    }
                    else if (Directory.Exists(path))
                    {
                        var option = chkSubfolders.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        IEnumerable<string> found = Enumerable.Empty<string>();

                        try
                        {
                            found = Directory.EnumerateFiles(path, "*.hwp", option)
                                .Concat(Directory.EnumerateFiles(path, "*.hwpx", option));
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Log("접근 권한이 없어 일부 폴더를 건너뛰었습니다: " + path);
                        }

                        foreach (var f in found)
                            AddFileIfSupported(f);
                    }
                }
                catch (Exception ex)
                {
                    Log("추가 실패: " + path + " / " + ex.Message);
                }
            }

            _files.Sort(StringComparer.CurrentCultureIgnoreCase);
            RefreshFileList();
            Log((_files.Count - before) + "개 파일을 추가했습니다.");
        }

        private void AddFileIfSupported(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".hwp" && ext != ".hwpx") return;

            string full = Path.GetFullPath(path);
            if (!_files.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                _files.Add(full);
        }

        private void RefreshFileList()
        {
            lvFiles.BeginUpdate();
            lvFiles.Items.Clear();
            foreach (var file in _files)
            {
                var item = new ListViewItem(file);
                item.ToolTipText = file;
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
            if (lvFiles.SelectedItems.Count == 0) return;

            var remove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListViewItem item in lvFiles.SelectedItems)
                remove.Add(item.Text);

            _files.RemoveAll(x => remove.Contains(x));
            RefreshFileList();
        }

        private void ClearFiles()
        {
            _files.Clear();
            RefreshFileList();
            progress.Value = 0;
            lblStatus.Text = "파일을 추가하세요.";
            txtLog.Clear();
        }

        private void UpdateOutputControls()
        {
            bool custom = rdoOutputFolder.Checked;
            txtOutputFolder.Enabled = custom && !_running;
            btnOutputFolder.Enabled = custom && !_running;
        }

        private void ChooseOutputFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "PDF를 저장할 폴더를 선택하세요.";
                if (Directory.Exists(txtOutputFolder.Text))
                    dlg.SelectedPath = txtOutputFolder.Text;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtOutputFolder.Text = dlg.SelectedPath;
                    rdoOutputFolder.Checked = true;
                }
            }
        }

        private void StartConversion()
        {
            if (_running) return;

            if (_files.Count == 0)
            {
                MessageBox.Show("변환할 HWP/HWPX 파일을 먼저 추가하세요.",
                    "파일 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (rdoOutputFolder.Checked)
            {
                if (string.IsNullOrWhiteSpace(txtOutputFolder.Text))
                {
                    MessageBox.Show("PDF를 저장할 폴더를 선택하세요.",
                        "출력 폴더", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try { Directory.CreateDirectory(txtOutputFolder.Text); }
                catch (Exception ex)
                {
                    MessageBox.Show("출력 폴더를 사용할 수 없습니다.\r\n" + ex.Message,
                        "출력 폴더 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _running = true;
            _cancelRequested = false;
            SetRunningUi(true);
            progress.Value = 0;
            txtLog.Clear();
            btnOpenOutput.Enabled = false;

            var filesSnapshot = _files.ToArray();
            string commonOutput = rdoOutputFolder.Checked ? txtOutputFolder.Text.Trim() : null;
            int existingMode = cboExisting.SelectedIndex;

            var t = new Thread(new ThreadStart(delegate()
            {
                RunConversion(filesSnapshot, commonOutput, existingMode);
            }));
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private void RunConversion(string[] files, string commonOutput, int existingMode)
        {
            dynamic hwp = null;
            int success = 0;
            int failed = 0;
            int skipped = 0;
            string lastOutputDir = "";
            bool securityOk = false;

            try
            {
                Ui(delegate { lblStatus.Text = "한글 자동화 환경을 확인하는 중..."; });

                Type hwpType = Type.GetTypeFromProgID(HwpProgId);
                if (hwpType == null)
                    throw new InvalidOperationException("한글 오토메이션(HWPFrame.HwpObject)을 찾을 수 없습니다. 한컴오피스 한글 설치 상태를 확인하세요.");

                hwp = Activator.CreateInstance(hwpType);
                securityOk = TryRegisterAnySecurityModule(hwp);

                try
                {
                    // 보안모듈이 없으면 승인창을 사용자가 볼 수 있도록 한글 창을 표시합니다.
                    hwp.XHwpWindows.Item(0).Visible = securityOk ? false : true;
                }
                catch { }

                if (!securityOk)
                    LogThread("보안모듈이 등록되지 않았습니다. 한글에서 파일 접근 승인창이 나타날 수 있습니다.");

                for (int i = 0; i < files.Length; i++)
                {
                    if (_cancelRequested)
                    {
                        LogThread("사용자 요청으로 작업을 중지했습니다.");
                        break;
                    }

                    string src = files[i];
                    string outDir = commonOutput ?? Path.GetDirectoryName(src);
                    Directory.CreateDirectory(outDir);

                    string desired = Path.Combine(outDir, Path.GetFileNameWithoutExtension(src) + ".pdf");
                    bool skip;
                    string pdfPath = ResolveOutputPath(desired, existingMode, out skip);

                    int index = i + 1;
                    Ui(delegate
                    {
                        lblStatus.Text = "변환 중 " + index + " / " + files.Length + " : " + Path.GetFileName(src);
                        progress.Value = Math.Max(0, Math.Min(100, (int)Math.Round((index - 1) * 100.0 / files.Length)));
                    });

                    if (skip)
                    {
                        skipped++;
                        LogThread("[건너뜀] " + src);
                        continue;
                    }

                    try
                    {
                        bool opened = Convert.ToBoolean(hwp.Open(
                            src,
                            "",
                            "lock:false;forceopen:true;versionwarning:false;suspendpassword:true;"
                        ));

                        if (!opened)
                            throw new InvalidOperationException("한글에서 문서를 열지 못했습니다.");

                        bool saved = Convert.ToBoolean(hwp.SaveAs(pdfPath, "PDF", ""));
                        if (!saved)
                            throw new InvalidOperationException("PDF 저장 API가 실패를 반환했습니다.");

                        WaitForPdf(pdfPath, 8000);

                        if (!File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
                            throw new InvalidOperationException("PDF 파일이 정상적으로 생성되지 않았습니다.");

                        success++;
                        lastOutputDir = outDir;
                        LogThread("[완료] " + Path.GetFileName(src) + " → " + pdfPath);
                    }
                    catch (Exception exFile)
                    {
                        failed++;
                        LogThread("[실패] " + src + " / " + CleanComMessage(exFile));
                    }
                    finally
                    {
                        try { hwp.Clear(1); } catch { }
                    }

                    Ui(delegate
                    {
                        progress.Value = Math.Max(0, Math.Min(100, (int)Math.Round(index * 100.0 / files.Length)));
                    });
                }
            }
            catch (Exception ex)
            {
                LogThread("[중단] " + CleanComMessage(ex));
                Ui(delegate
                {
                    MessageBox.Show(this,
                        "변환을 시작할 수 없습니다.\r\n\r\n" + CleanComMessage(ex),
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
            }
            finally
            {
                if (hwp != null)
                {
                    try { hwp.Clear(1); } catch { }
                    try { hwp.Quit(); } catch { }
                    try
                    {
                        if (Marshal.IsComObject(hwp))
                            Marshal.FinalReleaseComObject(hwp);
                    }
                    catch { }
                    hwp = null;
                }

                _lastOutputFolder = lastOutputDir;

                Ui(delegate
                {
                    _running = false;
                    SetRunningUi(false);
                    btnOpenOutput.Enabled = Directory.Exists(_lastOutputFolder);

                    if (!_cancelRequested)
                        progress.Value = 100;

                    lblStatus.Text = "완료 - 성공 " + success + "개 / 실패 " + failed + "개 / 건너뜀 " + skipped + "개";

                    if (success + failed + skipped > 0)
                    {
                        MessageBox.Show(this,
                            "PDF 변환 작업이 끝났습니다.\r\n\r\n" +
                            "성공: " + success + "개\r\n" +
                            "실패: " + failed + "개\r\n" +
                            "건너뜀: " + skipped + "개" +
                            (securityOk ? "" : "\r\n\r\n※ 보안모듈 미등록 상태로 실행했습니다."),
                            "변환 완료", MessageBoxButtons.OK,
                            failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }
                });
            }
        }

        private string ResolveOutputPath(string desired, int existingMode, out bool skip)
        {
            skip = false;

            if (!File.Exists(desired))
                return desired;

            // 0: 자동 이름 변경, 1: 건너뛰기, 2: 덮어쓰기
            if (existingMode == 1)
            {
                skip = true;
                return desired;
            }

            if (existingMode == 2)
            {
                try { File.Delete(desired); }
                catch (Exception ex) { throw new IOException("기존 PDF를 덮어쓸 수 없습니다: " + desired, ex); }
                return desired;
            }

            string dir = Path.GetDirectoryName(desired);
            string name = Path.GetFileNameWithoutExtension(desired);
            string ext = Path.GetExtension(desired);

            for (int i = 1; i < 10000; i++)
            {
                string candidate = Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(candidate))
                    return candidate;
            }

            throw new IOException("사용 가능한 출력 파일명을 만들 수 없습니다: " + desired);
        }

        private void WaitForPdf(string path, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length > 0)
                        return;
                }
                catch { }

                Thread.Sleep(200);
                waited += 200;
            }
        }

        private bool TryRegisterAnySecurityModule(dynamic hwp)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SecurityRegPath, false))
                {
                    if (key == null) return false;

                    foreach (string name in key.GetValueNames())
                    {
                        object value = key.GetValue(name);
                        string dll = value == null ? "" : Convert.ToString(value);

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dll))
                            continue;

                        if (!File.Exists(dll))
                            continue;

                        try
                        {
                            if (Convert.ToBoolean(hwp.RegisterModule("FilePathCheckDLL", name)))
                                return true;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return false;
        }

        private void SetupSecurityModule()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "한컴 공식 FilePathCheckerModuleExample.dll 선택";
                dlg.Filter = "한컴 보안모듈 DLL (*.dll)|*.dll";
                dlg.FileName = "FilePathCheckerModuleExample.dll";

                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    string installDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "HancomPdfBatch"
                    );
                    Directory.CreateDirectory(installDir);

                    string installed = Path.Combine(installDir, Path.GetFileName(dlg.FileName));
                    File.Copy(dlg.FileName, installed, true);

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SecurityRegPath))
                    {
                        if (key == null)
                            throw new InvalidOperationException("레지스트리 키를 만들 수 없습니다.");

                        key.SetValue(DefaultSecurityName, installed, RegistryValueKind.String);
                    }

                    MessageBox.Show(
                        "보안모듈을 현재 사용자 계정에 등록했습니다.\r\n\r\n" +
                        installed + "\r\n\r\n" +
                        "※ DLL은 한컴이 공식 배포한 보안모듈을 사용하세요.",
                        "설정 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("보안모듈 설정 실패\r\n\r\n" + ex.Message,
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void CheckEnvironment()
        {
            var lines = new List<string>();

            try
            {
                Type t = Type.GetTypeFromProgID(HwpProgId);
                lines.Add(t == null
                    ? "❌ 한글 오토메이션: 찾을 수 없음"
                    : "✅ 한글 오토메이션: 사용 가능");
            }
            catch (Exception ex)
            {
                lines.Add("❌ 한글 오토메이션 확인 실패: " + ex.Message);
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SecurityRegPath, false))
                {
                    if (key == null || key.GetValueNames().Length == 0)
                    {
                        lines.Add("⚠ 보안모듈: 등록된 항목 없음");
                    }
                    else
                    {
                        bool found = false;
                        foreach (string name in key.GetValueNames())
                        {
                            string path = Convert.ToString(key.GetValue(name));
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                lines.Add("✅ 보안모듈: " + name);
                                found = true;
                            }
                        }
                        if (!found)
                            lines.Add("⚠ 보안모듈: 레지스트리 값은 있으나 DLL 파일을 찾지 못함");
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add("⚠ 보안모듈 확인 실패: " + ex.Message);
            }

            lines.Add("");
            lines.Add("프로그램 비트수: " + (Environment.Is64BitProcess ? "64비트" : "32비트"));

            MessageBox.Show(string.Join("\r\n", lines.ToArray()),
                "환경 점검", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenLastOutput()
        {
            if (!Directory.Exists(_lastOutputFolder))
            {
                MessageBox.Show("열 수 있는 결과 폴더가 없습니다.",
                    "결과 폴더", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start("explorer.exe", "\"" + _lastOutputFolder + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show("폴더를 열 수 없습니다.\r\n" + ex.Message,
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetRunningUi(bool running)
        {
            btnAddFolder.Enabled = !running;
            btnAddFiles.Enabled = !running;
            btnRemove.Enabled = !running;
            btnClear.Enabled = !running;
            chkSubfolders.Enabled = !running;

            rdoSameFolder.Enabled = !running;
            rdoOutputFolder.Enabled = !running;
            cboExisting.Enabled = !running;

            btnStart.Enabled = !running;
            btnCancel.Enabled = running;
            btnCheck.Enabled = !running;
            btnSecurity.Enabled = !running;

            lvFiles.AllowDrop = !running;
            pnlDrop.AllowDrop = !running;
            lblDrop.AllowDrop = !running;

            UpdateOutputControls();
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

        private void LogThread(string message)
        {
            Ui(delegate { Log(message); });
        }

        private void Log(string message)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }

        private string CleanComMessage(Exception ex)
        {
            if (ex == null) return "알 수 없는 오류";
            Exception cur = ex;
            while (cur.InnerException != null) cur = cur.InnerException;
            return cur.Message;
        }
    }
}
