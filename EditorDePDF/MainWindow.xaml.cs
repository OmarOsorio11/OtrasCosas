using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.IO;
using System.Windows;

namespace EditorDePDF;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        _ = InitializeAsync();

        // En .NET 10, podemos usar el evento Loaded de la ventana 
        // o directamente el del editor.
        CodeEditor.Loaded += async (s, e) => await SetupEditor();
    }

    private async Task SetupEditor()
    {
        // En Monaco.Editor.WebView, a veces es necesario esperar a que el Core esté listo
        await CodeEditor.EnsureCoreWebView2Async();

        // Configuración
        await CodeEditor.SetLanguageAsync("csharp");
        await CodeEditor.SetThemeAsync("vs-dark");

        // El método para poner texto suele ser SetTextAsync o usar la propiedad Text
        await CodeEditor.SetTextAsync(GetDefaultCode());
    }

    private async Task InitializeAsync()
    {
        // Forzamos la inicialización del entorno del navegador
        await PdfViewer.EnsureCoreWebView2Async();
    }

    private async void BtnRender_Click(object? sender, RoutedEventArgs? e)
    {
        ErrorConsole.Clear();
        try
        {
            string currentCode = await CodeEditor.GetTextAsync();
            byte[] pdfData = await CompilePdfAsync(currentCode);

            if (pdfData != null)
            {
                // Guardar en temporal es más estable que Base64 en URLs largas
                string tempPath = Path.Combine(Path.GetTempPath(), "preview.pdf");
                await File.WriteAllBytesAsync(tempPath, pdfData);

                // WebView2 necesita la ruta absoluta con file://
                PdfViewer.CoreWebView2.Navigate(new Uri(tempPath).AbsoluteUri);
            }
        }
        catch (Exception ex)
        {
            ErrorConsole.Text = $"[ERROR]: {DateTime.Now:HH:mm:ss}\n{ex.Message}";
        }
    }

    private async Task<byte[]> CompilePdfAsync(string code)
    {
        var options = ScriptOptions.Default
            .WithReferences(typeof(PdfDocument).Assembly,
                            typeof(Document).Assembly,
                            typeof(ColorConstants).Assembly,
                            typeof(Table).Assembly,
                            typeof(MemoryStream).Assembly)
            .WithImports("System", "System.IO", "iText.Kernel.Pdf",
                         "iText.Layout", "iText.Layout.Element",
                         "iText.Layout.Properties", "iText.Kernel.Colors");

        // IMPORTANTE: .NET Scripting requiere que el código devuelva el valor directamente
        return await CSharpScript.EvaluateAsync<byte[]>(code, options);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F5)
        {
            BtnRender_Click(null, null);
            e.Handled = true; // Evita que el F5 haga otras cosas
        }
    }

    private string GetDefaultCode()
    {
        // Nota: No necesitas repetir los 'using' aquí si ya los pusiste en .WithImports
        return @"
using (var ms = new MemoryStream()) {
    using (var writer = new PdfWriter(ms)) {
        using (var pdf = new PdfDocument(writer)) {
            var doc = new Document(pdf);
            doc.Add(new Paragraph(""Generado desde .NET 10"").SetFontSize(20));
            
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 2, 8 }))
                .UseAllAvailableWidth();
            
            table.AddHeaderCell(""ID"");
            table.AddHeaderCell(""CONCEPTO"");
            table.AddCell(""01"");
            table.AddCell(""Prueba de compilación dinámica"");
            
            doc.Add(table);
            doc.Close();
        }
    }
    return ms.ToArray();
}";
    }
}