' --------------------------------
' --- FormMain.vb - 01/25/2016 ---
' --------------------------------

' ----------------------------------------------------------------------------------------------------
' 01/25/2016 - SBakker
'            - Display leftover SDR directories.
' 11/29/2014 - SBakker
'            - Only declare book names matching if they are both at least 30 chars. Calibre chops off
'              names at around 32 chars, but don't want "Illusions" and "Illusions II" saying they
'              match. Adjust length if necessary.
' 03/15/2014 - SBakker
'            - Added Bootstrapping to a a local area instead of using the ClickOnce install.
'            - Added the Settings Provider to save settings in the local area with the program.
'            - Added AboutMain.vb which shows the current path in the Status Bar.
' 01/07/2014 - SBakker
'            - Switched from MOBI to AZW3. Calibre now allows editing of AZW3, and most new books are
'              AZW3 format.
' 08/16/2012 - SBakker
'            - Use LastIndexOf(" - ") instead of IndexOf(" - ") to find Author, in case the
'              book name contains " - ".
' 04/06/2012 - SBakker
'            - Added AboutMain.vb form to this project.
' 04/03/2012 - SBakker
'            - Check the DateTime of Kindle MOBI files against the Calibre files, to see if
'              an update is needed.
' 03/13/2012 - SBakker
'            - Check "cover.jpg" datetime and make sure MOBI file has been updated later.
'            - Display mismatched Timestamps between the books and covers, and metadata.opf.
'              Note that you have to change each book individually in Calibre to get the
'              metadata.opf to update. Blah!
' 09/05/2011 - SBakker
'            - Working on BookCleaner.
' ----------------------------------------------------------------------------------------------------

Imports System.IO

Public Class FormMain

    Private Shared ReadOnly ObjName As String = System.Reflection.MethodBase.GetCurrentMethod().ReflectedType.FullName

    Private CalibreCount As Integer = 0
    Private KindleCount As Integer = 0

    Private Sub FormMain_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

        Static FuncName As String = ObjName + "." + System.Reflection.MethodBase.GetCurrentMethod().Name

        Try
            If Arena_Bootstrap.BootstrapClass.CopyProgramsToLaunchPath Then
                Me.Close()
                Exit Sub
            End If
        Catch ex As Exception
            MessageBox.Show(FuncName + vbCrLf + ex.Message, My.Application.Info.AssemblyName, MessageBoxButtons.OK)
            Me.Close()
            Exit Sub
        End Try

        ' --- First call Upgrade to load setting from last version ---
        If My.Settings.CallUpgrade Then
            My.Settings.Upgrade()
            My.Settings.CallUpgrade = False
            My.Settings.Save()
        End If

        TextBoxFromPath.Text = My.Settings.DefaultPath
        TextBoxKindlePath.Text = My.Settings.KindlePath

    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles ExitToolStripMenuItem.Click
        Me.Close()
    End Sub

    Private Sub ButtonStart_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles ButtonStart.Click
        If TextBoxFromPath.Text = "" Then Exit Sub
        If Not Directory.Exists(TextBoxFromPath.Text) Then Exit Sub
        CalibreCount = 0
        KindleCount = 0
        UpdateStatusLabel()
        ButtonStart.Enabled = False
        My.Settings.DefaultPath = TextBoxFromPath.Text
        My.Settings.KindlePath = TextBoxKindlePath.Text
        My.Settings.Save()
        TextBoxResults.Text = ""
        Dim BookInfoList As New List(Of BookInfo)
        ' --- Read through all Calibre files looking for files needing updating ---
        Dim AuthorDirs() As String = Directory.GetDirectories(TextBoxFromPath.Text)
        For Each CurrAuthorDir As String In AuthorDirs
            Dim BookDirs() As String = Directory.GetDirectories(CurrAuthorDir)
            For Each CurrBookDir As String In BookDirs
                CalibreCount += 1
                UpdateStatusLabel()
                TextBoxFileName.Text = CurrBookDir
                Application.DoEvents()
                ' --- Look for OPF files with newer data than ebook files ---
                Dim LatestOpfDate As Date = Date.MinValue
                Dim LatestCoverDate As Date = Date.MinValue
                Dim LatestZipDate As Date = Date.MinValue
                Dim LatestOtherDate As Date = Date.MinValue
                Dim AllFiles() As String = Directory.GetFiles(CurrBookDir, "*.*")
                For Each currFile As String In AllFiles
                    Dim tempDate As Date = File.GetLastWriteTimeUtc(currFile)
                    If currFile.EndsWith("metadata.opf") Then
                        LatestOpfDate = tempDate
                    ElseIf currFile.EndsWith("cover.jpg") Then
                        LatestCoverDate = tempDate
                    ElseIf currFile.EndsWith(".zip") Then
                        If LatestZipDate < tempDate Then
                            LatestZipDate = tempDate
                        End If
                    ElseIf LatestOtherDate < tempDate Then
                        LatestOtherDate = tempDate
                    End If
                    ' --- Look for AZW3 files to add to comparison list ---
                    If currFile.EndsWith(".azw3") Then
                        Dim NewBookInfo As New BookInfo
                        With NewBookInfo
                            .Filename = currFile
                            .FileDateTime = File.GetLastWriteTimeUtc(currFile)
                        End With
                        BookInfoList.Add(NewBookInfo)
                    End If
                Next
                If LatestOpfDate > LatestOtherDate OrElse
                    LatestCoverDate > LatestOtherDate OrElse
                    LatestZipDate > LatestOtherDate Then
                    TextBoxResults.AppendText(CurrBookDir + vbCrLf)
                    Continue For
                End If
            Next
        Next
        ' --- Check the Kindle files to see if they are out of date ---
        If Directory.Exists(TextBoxKindlePath.Text) Then
            Dim KindleAuthorDirs() As String = Directory.GetDirectories(TextBoxKindlePath.Text)
            For Each CurrAuthorDir As String In KindleAuthorDirs
                TextBoxFileName.Text = CurrAuthorDir
                Application.DoEvents()
                ' --- Look for zip files with missing or outdated AZW3 files ---
                Dim MobiFiles() As String = Directory.GetFiles(CurrAuthorDir, "*.azw3")
                For Each CurrMobiFile As String In MobiFiles
                    KindleCount += 1
                    UpdateStatusLabel()
                    Dim FileFound As Boolean = False
                    Dim TempBookInfo As BookInfo = Nothing
                    For Each TempBookInfo In BookInfoList
                        If BookNamesMatch(TempBookInfo.Filename, CurrMobiFile) Then
                            FileFound = True
                            Dim CalibreDate As DateTime = File.GetLastWriteTimeUtc(TempBookInfo.Filename)
                            Dim KindleDate As DateTime = File.GetLastWriteTimeUtc(CurrMobiFile)
                            If CalibreDate > KindleDate Then
                                TextBoxResults.AppendText("Needs update: " + TempBookInfo.Filename.Substring(TempBookInfo.Filename.LastIndexOf("\") + 1) + vbCrLf)
                            End If
                            Exit For
                        End If
                    Next
                    If Not FileFound Then
                        ' --- File is on Kindle but not in Calibre ---
                        TextBoxResults.AppendText("Not in Calibre: " + CurrMobiFile.Substring(TextBoxKindlePath.Text.Length + 1) + vbCrLf)
                    End If
                Next
            Next
        End If
        ' --- Check for leftover SDR directories ---
        If Directory.Exists(TextBoxKindlePath.Text) Then
            Dim KindleDirs() As String = Directory.GetDirectories(TextBoxKindlePath.Text)
            For Each CurrDir As String In KindleDirs
                TextBoxFileName.Text = CurrDir
                Application.DoEvents()
                If CurrDir.EndsWith(".sdr") Then
                    If Not File.Exists(CurrDir.Replace(".sdr", ".azw")) AndAlso
                            Not File.Exists(CurrDir.Replace(".sdr", ".azw3")) AndAlso
                            Not File.Exists(CurrDir.Replace(".sdr", ".txt")) Then
                        TextBoxResults.AppendText("Leftover setting directory: " + CurrDir + vbCrLf)
                    End If
                Else
                    Dim KindleDirs2() As String = Directory.GetDirectories(CurrDir)
                    For Each CurrDir2 As String In KindleDirs2
                        TextBoxFileName.Text = CurrDir2
                        Application.DoEvents()
                        If CurrDir2.EndsWith(".sdr") Then
                            If Not File.Exists(CurrDir2.Replace(".sdr", ".azw")) AndAlso
                                    Not File.Exists(CurrDir2.Replace(".sdr", ".azw3")) AndAlso
                                    Not File.Exists(CurrDir2.Replace(".sdr", ".txt")) Then
                                TextBoxResults.AppendText("Leftover setting directory: " + CurrDir2 + vbCrLf)
                            End If
                        End If
                    Next
                End If
            Next
        End If
        ' --- Done ---
        ToolStripStatusLabelMain.Text += " - Done"
        ButtonStart.Enabled = True
    End Sub

    Private Function BookNamesMatch(ByVal CalibreFilename As String, ByVal KindleFilename As String) As Boolean
        CalibreFilename = CalibreFilename.ToLower
        KindleFilename = KindleFilename.ToLower
        CalibreFilename = CalibreFilename.Substring(CalibreFilename.LastIndexOf("\"c) + 1).Replace(".azw3", "")
        Dim CalibreAuthor As String = CalibreFilename.Substring(CalibreFilename.LastIndexOf(" - ") + 3)
        If CalibreAuthor.Contains("_") Then
            CalibreAuthor = CalibreAuthor.Replace("_", ".")
        End If
        If CalibreAuthor.Contains(",") Then
            CalibreAuthor = CalibreAuthor.Substring(CalibreAuthor.IndexOf(",") + 1).Trim + " " + CalibreAuthor.Substring(0, CalibreAuthor.IndexOf(",")).Trim
        End If
        Dim CalibreBook As String = CalibreFilename.Substring(0, CalibreFilename.LastIndexOf(" - "))
        KindleFilename = KindleFilename.Substring(TextBoxKindlePath.Text.Length + 1).Replace(".azw3", "")
        Dim KindleAuthor As String = KindleFilename.Substring(0, KindleFilename.IndexOf("\"c))
        If KindleAuthor.Contains("&") Then
            ' --- Only the first author is in the CalibreAuthor, so take others out of the KindleAuthor ---
            KindleAuthor = KindleAuthor.Substring(0, KindleAuthor.IndexOf("&")).Trim
        End If
        If KindleAuthor.Contains("_") Then
            KindleAuthor = KindleAuthor.Replace("_", ".")
        End If
        If KindleAuthor.Contains(",") Then
            KindleAuthor = KindleAuthor.Substring(KindleAuthor.IndexOf(",") + 1).Trim + " " + KindleAuthor.Substring(0, KindleAuthor.IndexOf(",")).Trim
        End If
        Dim KindleBook As String = KindleFilename.Substring(KindleFilename.IndexOf("\"c) + 1)
        If KindleBook.EndsWith(", a") Then KindleBook = "a " + KindleBook.Substring(0, KindleBook.Length - ", a".Length)
        If KindleBook.EndsWith(", an") Then KindleBook = "an " + KindleBook.Substring(0, KindleBook.Length - ", an".Length)
        If KindleBook.EndsWith(", the") Then KindleBook = "the " + KindleBook.Substring(0, KindleBook.Length - ", the".Length)
        If CalibreAuthor = KindleAuthor Then
            If KindleBook = CalibreBook Then
                Return True
            End If
            ' --- Sometimes the Calibre book name is chopped off at around 32 chars ---
            If CalibreBook.Length >= 30 AndAlso KindleBook.StartsWith(CalibreBook) Then
                Return True
            End If
        End If
        Return False
    End Function

    Private Sub AboutToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles AboutToolStripMenuItem.Click
        Dim TempAbout As New AboutMain
        TempAbout.ShowDialog()
        TempAbout = Nothing
    End Sub

    Private Sub UpdateStatusLabel()
        ToolStripStatusLabelMain.Text = "Calibre Books Checked: " + CalibreCount.ToString +
                                        " - Kindle Books Checked: " + KindleCount.ToString
    End Sub

End Class
