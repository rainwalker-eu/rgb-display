Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading
Imports Avalonia
Imports Avalonia.Controls
Imports Avalonia.Interactivity
Imports Avalonia.Layout
Imports Avalonia.Media
Imports Avalonia.Media.Imaging
Imports Avalonia.Platform
Imports Avalonia.Platform.Storage
Imports Avalonia.Threading

Public Class MainWindow
    Inherits Window

    Private Const DefaultProgram As String =
        "BRIGHT 3
FRAME 0
SEC 1
SCROLLLEFT 0 2 30
SCROLLRIGHT 0 2 30
SEC 1
FILL 255 255 255
MSEC 10
CLEAR
MSEC 250
FILL 255 0 0
MSEC 10
CLEAR
SEC 1"

    Private Const CommandHelp As String =
        "CLEAR - clears screen
FILL 20 10 0 - fill screen with R=20, G=10, B=0, maximum is 255
BRIGHT 10 - set brightness 10% (for frames)
FRAME 1 - show frame 1 (horizontal from 32 to 63 cols), maximum is 6
FROMX 10 - show frame with offset (from 10 to 41 cols)
WAIT - wait indefinitely (for button; click the emulated display)
MSEC 100 - wait 100 ms, maximum is 255
SEC 2 - wait 2 seconds
SCROLLLEFT 1 2 3 scrolls frames left from 1 to 2 with delay 3 ms
SCROLLRIGHT 1 2 3 scrolls frames right from 2 to 1 with delay 3 ms"

    Private ReadOnly tbPath As New TextBox
    Private ReadOnly bSelectFile As New Button With {.Content = "Open image"}
    Private ReadOnly lError As New TextBlock With {.Foreground = Brushes.Red, .TextWrapping = TextWrapping.Wrap}
    Private ReadOnly imgPreview As New Image With {.Stretch = Stretch.Uniform}
    Private ReadOnly tbProgram As New TextBox With {.AcceptsReturn = True, .Height = 240, .Text = DefaultProgram}
    Private ReadOnly bEmulate As New Button With {.Content = "Emulate"}
    Private ReadOnly emulatorView As New EmulatorView With {.HorizontalAlignment = HorizontalAlignment.Left}
    Private ReadOnly cbPorts As New ComboBox With {.HorizontalAlignment = HorizontalAlignment.Stretch}
    Private ReadOnly bRefreshPorts As New Button With {.Content = "Refresh"}
    Private ReadOnly bSendToBoard As New Button With {.Content = "Send to board", .IsEnabled = False}
    Private ReadOnly bStart As New Button With {.Content = "Start program"}
    Private ReadOnly bStop As New Button With {.Content = "Clear and stop"}
    Private ReadOnly bWriteToFlash As New Button With {.Content = "Write data to flash"}

    Private pixels As System.Drawing.Color(,)
    Private emulationThread As Thread
    Private ReadOnly emulator As New DisplayEmulator
    Private ReadOnly display As New DisplaySender

    Public Sub New()
        Title = "RGB3210"
        Width = 960
        Height = 640
        Content = BuildLayout()

        emulator.ShowScreen = Sub(screen)
                                  ' The emulator reuses its array, so the UI thread gets a copy
                                  Dim frame = CType(screen.Clone(), System.Drawing.Color(,))
                                  Dispatcher.UIThread.Post(Sub() emulatorView.Show(frame))
                              End Sub

        AddHandler tbPath.TextChanged, Sub() LoadImage()
        AddHandler bSelectFile.Click, AddressOf SelectFile
        AddHandler bEmulate.Click, AddressOf ToggleEmulation
        AddHandler emulatorView.PointerPressed, Sub() emulator.PressButton()
        AddHandler bRefreshPorts.Click, Sub() RefreshPorts()
        AddHandler bSendToBoard.Click, Sub() Send(Function() {DisplayProtocol.ProgramBytes(DisplayCommand.ParseProgram(tbProgram.Text)),
                                                              DisplayProtocol.PixelBytes(pixels)})
        AddHandler bStart.Click, Sub() Send(Function() {DisplayProtocol.StartProgram()})
        AddHandler bStop.Click, Sub() Send(Function() {DisplayProtocol.StopProgram(), DisplayProtocol.ClearDisplay()})
        AddHandler bWriteToFlash.Click, Sub() Send(Function() {DisplayProtocol.SaveToFlash()})
        AddHandler Closing, Sub() emulator.RequestStop()

        RefreshPorts()
        tbPath.Text = Path.Combine(AppContext.BaseDirectory, "test.bmp")
    End Sub

    Private Function BuildLayout() As Control
        Dim fileRow As New Grid With {.ColumnDefinitions = New ColumnDefinitions("*,Auto"), .ColumnSpacing = 8}
        fileRow.Children.Add(tbPath)
        Grid.SetColumn(bSelectFile, 1)
        fileRow.Children.Add(bSelectFile)

        RenderOptions.SetBitmapInterpolationMode(imgPreview, BitmapInterpolationMode.None)
        Dim preview As New Border With {.Height = 100, .BorderBrush = Brushes.Gray, .BorderThickness = New Thickness(1),
                                        .Background = Brushes.Black, .Child = imgPreview}

        Dim programColumn As New StackPanel With {.Spacing = 6}
        programColumn.Children.Add(New TextBlock With {.Text = "Program"})
        programColumn.Children.Add(tbProgram)
        programColumn.Children.Add(bEmulate)
        programColumn.Children.Add(emulatorView)

        Dim helpColumn As New StackPanel With {.Spacing = 6}
        helpColumn.Children.Add(New TextBlock With {.Text = "Commands"})
        helpColumn.Children.Add(New TextBlock With {.Text = CommandHelp, .TextWrapping = TextWrapping.Wrap})

        Dim portRow As New Grid With {.ColumnDefinitions = New ColumnDefinitions("*,Auto"), .ColumnSpacing = 8}
        portRow.Children.Add(cbPorts)
        Grid.SetColumn(bRefreshPorts, 1)
        portRow.Children.Add(bRefreshPorts)

        Dim boardColumn As New StackPanel With {.Spacing = 6}
        boardColumn.Children.Add(New TextBlock With {.Text = "Port"})
        boardColumn.Children.Add(portRow)
        For Each button In {bSendToBoard, bStart, bStop, bWriteToFlash}
            button.HorizontalAlignment = HorizontalAlignment.Stretch
            button.HorizontalContentAlignment = HorizontalAlignment.Center
            boardColumn.Children.Add(button)
        Next

        Dim columns As New Grid With {.ColumnDefinitions = New ColumnDefinitions("320,*,240"), .ColumnSpacing = 12}
        columns.Children.Add(programColumn)
        Grid.SetColumn(helpColumn, 1)
        columns.Children.Add(helpColumn)
        Grid.SetColumn(boardColumn, 2)
        columns.Children.Add(boardColumn)

        Dim root As New StackPanel With {.Margin = New Thickness(12), .Spacing = 8}
        root.Children.Add(fileRow)
        root.Children.Add(lError)
        root.Children.Add(preview)
        root.Children.Add(columns)
        Return root
    End Function

    Private Sub RefreshPorts()
        Dim selected = TryCast(cbPorts.SelectedItem, String)
        Dim ports = DisplayPorts.All()
        cbPorts.ItemsSource = ports
        Dim preferred = If(selected, DisplayPorts.DefaultPort())
        cbPorts.SelectedItem = If(ports.Contains(preferred), preferred, ports.FirstOrDefault())
    End Sub

    Private Sub LoadImage()
        Try
            imgPreview.Source = Nothing
            bSendToBoard.IsEnabled = False
            Dim bmp As New Bitmap(tbPath.Text)
            Dim loaded = ReadPixels(bmp)
            If loaded.GetLength(1) <> DisplayEmulator.Height OrElse loaded.GetLength(0) < DisplayEmulator.Width Then
                Throw New Exception("Image height must be 10 pixels and width must be at least 32 pixels")
            End If
            pixels = loaded
            emulator.Pixels = loaded
            imgPreview.Source = bmp
            lError.Text = ""
            bSendToBoard.IsEnabled = True
        Catch ex As Exception
            lError.Text = $"Error loading image: {ex.Message}"
        End Try
    End Sub

    ' Decoded bitmaps come as 32-bit BGRA or RGBA depending on the platform
    Private Shared Function ReadPixels(bmp As Bitmap) As System.Drawing.Color(,)
        Dim size = bmp.PixelSize
        Dim format = bmp.Format.GetValueOrDefault()
        Dim bgra = format = PixelFormats.Bgra8888
        If Not bgra AndAlso format <> PixelFormats.Rgba8888 Then Throw New Exception($"Unsupported pixel format {format}")
        Dim stride = size.Width * 4
        Dim data(stride * size.Height - 1) As Byte
        Dim handle = GCHandle.Alloc(data, GCHandleType.Pinned)
        Try
            bmp.CopyPixels(New PixelRect(size), handle.AddrOfPinnedObject(), data.Length, stride)
        Finally
            handle.Free()
        End Try
        Dim result(size.Width - 1, size.Height - 1) As System.Drawing.Color
        For x = 0 To size.Width - 1
            For y = 0 To size.Height - 1
                Dim i = y * stride + x * 4
                result(x, y) = System.Drawing.Color.FromArgb(data(i + If(bgra, 2, 0)), data(i + 1), data(i + If(bgra, 0, 2)))
            Next
        Next
        Return result
    End Function

    Private Async Sub SelectFile(sender As Object, e As RoutedEventArgs)
        Dim files = Await StorageProvider.OpenFilePickerAsync(New FilePickerOpenOptions With {
            .Title = "Open image",
            .FileTypeFilter = {New FilePickerFileType("Image files") With {.Patterns = {"*.bmp", "*.jpg", "*.jpeg", "*.png", "*.gif"}},
                               FilePickerFileTypes.All}})
        Dim path = files.FirstOrDefault()?.TryGetLocalPath()
        If path IsNot Nothing Then tbPath.Text = path
    End Sub

    Private Sub ToggleEmulation(sender As Object, e As RoutedEventArgs)
        If emulationThread IsNot Nothing Then
            emulator.RequestStop()
            bEmulate.Content = "Stopping..."
            Return
        End If
        Try
            Dim commands = DisplayCommand.ParseProgram(tbProgram.Text)
            lError.Text = ""
            bEmulate.Content = "Stop"
            emulationThread = New Thread(Sub()
                                             emulator.Run(commands)
                                             Dispatcher.UIThread.Post(Sub()
                                                                          emulationThread = Nothing
                                                                          bEmulate.Content = "Emulate"
                                                                      End Sub)
                                         End Sub) With {.IsBackground = True}
            emulationThread.Start()
        Catch ex As Exception
            lError.Text = ex.Message
        End Try
    End Sub

    ' Sending takes a second or so for a full picture, so it runs off the UI thread
    Private Async Sub Send(build As Func(Of Byte()()))
        Try
            Dim port = TryCast(cbPorts.SelectedItem, String)
            If port Is Nothing Then Throw New Exception("No serial port selected")
            display.SerialPort = port
            Dim data = build()
            lError.Text = ""
            Await Task.Run(Sub() display.Send(data))
        Catch ex As Exception
            lError.Text = ex.Message
        End Try
    End Sub
End Class
