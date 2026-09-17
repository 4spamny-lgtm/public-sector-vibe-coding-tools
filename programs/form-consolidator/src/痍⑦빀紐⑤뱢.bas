Option Explicit

' =====================================================================
'  서식 취합 도구 (시군명 매칭 방식)
'  - 빈 서식에서 시군명이 있는 열을 자동으로 찾고,
'  - 제출 파일에서 같은 시군명을 찾아 그 행의 값을 빈 서식의 해당 시군 행에 옮겨 담습니다.
'  - 제출 파일에서 다른 시군 행을 지우거나 위아래를 잘라내도 동작합니다.
' =====================================================================

Private Function Norm(v As Variant) As String
    ' 비교용: 공백 제거, 앞뒤 정리
    Norm = Replace(Replace(Replace(CStr(v), " ", ""), vbTab, ""), Chr(160), "")
End Function

Private Function FindKeyColumn(ws As Worksheet) As Long
    ' "~시/~군/~구" 로 끝나는 글자 셀이 가장 많은 열 = 시군명 열
    Dim c As Range, cnt() As Long, col As Long, best As Long, bestCnt As Long, s As String
    ReDim cnt(1 To ws.UsedRange.Columns.Count + ws.UsedRange.Column)
    For Each c In ws.UsedRange
        If VarType(c.Value) = vbString Then
            s = Norm(c.Value)
            If Len(s) >= 2 And Len(s) <= 6 Then
                If Right(s, 1) = "시" Or Right(s, 1) = "군" Or Right(s, 1) = "구" Then
                    cnt(c.Column) = cnt(c.Column) + 1
                End If
            End If
        End If
    Next c
    For col = LBound(cnt) To UBound(cnt)
        If cnt(col) > bestCnt Then bestCnt = cnt(col): best = col
    Next col
    If bestCnt < 2 Then best = 0
    FindKeyColumn = best
End Function

Sub 취합하기()
    Dim fdFile As FileDialog, fdFolder As FileDialog
    Dim tplPath As String, folderPath As String, fName As String, outPath As String
    Dim wbTpl As Workbook, wbOut As Workbook, wbSrc As Workbook
    Dim wsTpl As Worksheet, wsSrc As Worksheet, wsOut As Worksheet
    Dim keyCol As Long, keyRow As Long, srcRow As Long, srcCol As Long, colOff As Long
    Dim c As Range, cell As Range, col As Long, firstCol As Long, lastCol As Long
    Dim keys As Object, k As String, ans As String
    Dim nFile As Long, nCell As Long, nKey As Long
    Dim conflicts As String, skipped As String, notFound As String, filled As Object
    On Error GoTo ErrHandler

    ' 1) 빈 서식 선택
    Set fdFile = Application.FileDialog(msoFileDialogFilePicker)
    fdFile.Title = "1단계: 시군에 배포했던 '빈 서식' 파일을 선택하세요"
    fdFile.Filters.Clear
    fdFile.Filters.Add "Excel 파일", "*.xlsx;*.xls;*.xlsm"
    fdFile.AllowMultiSelect = False
    If fdFile.Show = 0 Then Exit Sub
    tplPath = fdFile.SelectedItems(1)

    ' 2) 제출 파일 폴더 선택
    Set fdFolder = Application.FileDialog(msoFileDialogFolderPicker)
    fdFolder.Title = "2단계: 시군이 제출한 파일들이 모여 있는 폴더를 선택하세요"
    If fdFolder.Show = 0 Then Exit Sub
    folderPath = fdFolder.SelectedItems(1) & "\"

    Application.ScreenUpdating = False
    Application.DisplayAlerts = False

    ' 3) 빈 서식 열고 복사본을 결과 파일로
    Set wbTpl = Workbooks.Open(tplPath, ReadOnly:=True)
    wbTpl.Sheets.Copy
    Set wbOut = ActiveWorkbook
    Set filled = CreateObject("Scripting.Dictionary")   ' 이미 값이 들어간 시군 기록

    ' 4) 제출 파일 순회
    fName = Dir(folderPath & "*.xls*")
    Do While fName <> ""
        If LCase(folderPath & fName) <> LCase(tplPath) _
           And fName <> ThisWorkbook.Name _
           And Left(fName, 4) <> "취합결과" _
           And Left(fName, 2) <> "~$" Then
            Set wbSrc = Workbooks.Open(folderPath & fName, ReadOnly:=True, UpdateLinks:=0)

            For Each wsTpl In wbTpl.Worksheets
                keyCol = FindKeyColumn(wsTpl)
                If keyCol = 0 Then GoTo NextSheet          ' 시군명 열이 없는 시트(안내문 등)는 건너뜀

                Set wsSrc = Nothing
                On Error Resume Next
                Set wsSrc = wbSrc.Worksheets(wsTpl.Name)
                If wsSrc Is Nothing And wbSrc.Worksheets.Count = 1 Then Set wsSrc = wbSrc.Worksheets(1)
                On Error GoTo ErrHandler
                If wsSrc Is Nothing Then
                    skipped = skipped & vbCrLf & "  " & fName & " (시트 '" & wsTpl.Name & "' 없음)"
                    GoTo NextSheet
                End If
                Set wsOut = wbOut.Worksheets(wsTpl.Name)
                firstCol = wsTpl.UsedRange.Column
                lastCol = firstCol + wsTpl.UsedRange.Columns.Count - 1

                ' 빈 서식의 시군명 -> 행 번호 사전
                Set keys = CreateObject("Scripting.Dictionary")
                For Each c In wsTpl.Columns(keyCol).Cells
                    If c.Row > wsTpl.UsedRange.Row + wsTpl.UsedRange.Rows.Count Then Exit For
                    If VarType(c.Value) = vbString Then
                        k = Norm(c.Value)
                        If Len(k) > 0 And Not keys.Exists(k) Then keys.Add k, c.Row
                    End If
                Next c

                ' 제출 파일에서 시군명 찾기 (어느 열에 있든 상관없음)
                For Each cell In wsSrc.UsedRange
                    If VarType(cell.Value) = vbString Then
                        k = Norm(cell.Value)
                        If keys.Exists(k) Then
                            keyRow = keys(k)
                            srcRow = cell.Row
                            colOff = cell.Column - keyCol          ' 열이 통째로 밀린 경우 보정

                            For col = firstCol To lastCol
                                If col <> keyCol Then
                                    srcCol = col + colOff
                                    If srcCol >= 1 Then
                                        If Not IsEmpty(wsSrc.Cells(srcRow, srcCol).Value) _
                                           And IsEmpty(wsTpl.Cells(keyRow, col).Value) Then
                                            If IsEmpty(wsOut.Cells(keyRow, col).Value) Then
                                                wsOut.Cells(keyRow, col).Value = wsSrc.Cells(srcRow, srcCol).Value
                                                nCell = nCell + 1
                                                If Not filled.Exists(wsTpl.Name & "|" & k) Then
                                                    filled.Add wsTpl.Name & "|" & k, fName
                                                    nKey = nKey + 1
                                                End If
                                            ElseIf CStr(wsOut.Cells(keyRow, col).Value) <> CStr(wsSrc.Cells(srcRow, srcCol).Value) Then
                                                conflicts = conflicts & vbCrLf & "  " & k & " " & wsTpl.Cells(keyRow, col).Address(False, False) & _
                                                            " : " & filled(wsTpl.Name & "|" & k) & " 값 유지, " & fName & " 값 무시"
                                            End If
                                        End If
                                    End If
                                End If
                            Next col
                        End If
                    End If
                Next cell
NextSheet:
            Next wsTpl

            wbSrc.Close SaveChanges:=False
            Set wbSrc = Nothing
            nFile = nFile + 1
        End If
        fName = Dir
    Loop

    ' 5) 값이 들어오지 않은 시군 목록
    For Each wsTpl In wbTpl.Worksheets
        keyCol = FindKeyColumn(wsTpl)
        If keyCol > 0 Then
            For Each c In wsTpl.Columns(keyCol).Cells
                If c.Row > wsTpl.UsedRange.Row + wsTpl.UsedRange.Rows.Count Then Exit For
                If VarType(c.Value) = vbString Then
                    k = Norm(c.Value)
                    If Len(k) > 0 And Not filled.Exists(wsTpl.Name & "|" & k) Then
                        If Right(k, 1) = "시" Or Right(k, 1) = "군" Or Right(k, 1) = "구" Then
                            notFound = notFound & IIf(notFound = "", "", ", ") & k
                        End If
                    End If
                End If
            Next c
        End If
    Next wsTpl

    ' 6) 저장 및 결과 안내
    outPath = folderPath & "취합결과_" & Format(Now, "yyyymmdd_hhmm") & ".xlsx"
    wbOut.SaveAs outPath, FileFormat:=xlOpenXMLWorkbook
    wbTpl.Close SaveChanges:=False
    Application.ScreenUpdating = True
    Application.DisplayAlerts = True

    If nFile = 0 Then
        wbOut.Close SaveChanges:=False
        Kill outPath
        MsgBox "선택한 폴더에 제출 파일이 없습니다.", vbExclamation
        Exit Sub
    End If

    MsgBox "총 " & nFile & "개 파일에서 " & nKey & "개 시군, " & nCell & "개 칸을 취합했습니다." & vbCrLf & vbCrLf & _
           "결과 파일: " & outPath & _
           IIf(notFound <> "", vbCrLf & vbCrLf & "아직 값이 없는 시군: " & notFound, "") & _
           IIf(conflicts <> "", vbCrLf & vbCrLf & "같은 칸에 서로 다른 값:" & conflicts, "") & _
           IIf(skipped <> "", vbCrLf & vbCrLf & "건너뛴 항목:" & skipped, ""), vbInformation, "취합 완료"
    Exit Sub

ErrHandler:
    Application.ScreenUpdating = True
    Application.DisplayAlerts = True
    On Error Resume Next
    If Not wbSrc Is Nothing Then wbSrc.Close SaveChanges:=False
    If Not wbTpl Is Nothing Then wbTpl.Close SaveChanges:=False
    MsgBox "오류가 발생했습니다." & vbCrLf & "파일: " & fName & vbCrLf & "내용: " & Err.Description, vbCritical, "취합 중단"
End Sub
