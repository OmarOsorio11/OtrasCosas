using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace RecorteDeImagenes
{
    public partial class MainWindow : Window
    {
        private Point _startPoint;
        private bool _isDragging = false;

        // Referencia a la imagen original (nunca cambia)
        private BitmapSource _originalBitmap;

        // Pila para el historial de recortes (Deshacer paso a paso)
        private Stack<BitmapSource> _historialRecortes = new Stack<BitmapSource>();

        private string _rutaImagen = @"C:\Users\omar.osorio\Desktop\ImagenEscaneada_58733157-e644-4ffe-9bf5-a35d0b78bf65.png";

        public MainWindow()
        {
            InitializeComponent();
            CargarImagen();
        }

        private void CargarImagen()
        {
            if (File.Exists(_rutaImagen))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_rutaImagen);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                _originalBitmap = bitmap;
                ImgTarget.Source = bitmap;

                ImgTarget.SizeChanged += (s, e) =>
                {
                    FullRect.Rect = new Rect(0, 0, ImgTarget.ActualWidth, ImgTarget.ActualHeight);
                };
            }
            else
            {
                MessageBox.Show("Archivo no encontrado.");
            }
        }

        #region Interacción del Mouse
        private void CanvasNativo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Thumb) return;
            _isDragging = true;
            _startPoint = e.GetPosition(CanvasNativo);
            SelectionBorder.Visibility = Visibility.Visible;
        }

        private void CanvasNativo_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            Point p = e.GetPosition(CanvasNativo);
            double x = Math.Clamp(p.X, 0, ImgTarget.ActualWidth);
            double y = Math.Clamp(p.Y, 0, ImgTarget.ActualHeight);
            ActualizarVisuales(new Rect(_startPoint, new Point(x, y)));
        }

        private void CanvasNativo_MouseUp(object sender, MouseButtonEventArgs e) => _isDragging = false;
        #endregion

        #region Lógica de Recorte y Botones

        private void BtnApplyCrop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectionRect.Rect.Width < 10) return;

            // Guardamos el estado actual en la pila antes de recortar
            _historialRecortes.Push((BitmapSource)ImgTarget.Source);

            BitmapSource source = (BitmapSource)ImgTarget.Source;
            double scaleX = source.PixelWidth / ImgTarget.ActualWidth;
            double scaleY = source.PixelHeight / ImgTarget.ActualHeight;

            Int32Rect rect = new Int32Rect(
                (int)(SelectionRect.Rect.X * scaleX), (int)(SelectionRect.Rect.Y * scaleY),
                (int)(SelectionRect.Rect.Width * scaleX), (int)(SelectionRect.Rect.Height * scaleY));

            try
            {
                ImgTarget.Source = new CroppedBitmap(source, rect);
                BtnClear_Click(null, null);
            }
            catch
            {
                _historialRecortes.Pop(); // Si falla, quitamos de la pila
                MessageBox.Show("Selección inválida.");
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_historialRecortes.Count > 0)
            {
                ImgTarget.Source = _historialRecortes.Pop();
                BtnClear_Click(null, null);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap != null)
            {
                ImgTarget.Source = _originalBitmap;
                _historialRecortes.Clear(); // Limpiamos historial
                BtnClear_Click(null, null);
            }
        }

        private void BtnAcceptSave_Click(object sender, RoutedEventArgs e)
        {
            BitmapSource source = (BitmapSource)ImgTarget.Source;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Resultado_Final.png");

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (FileStream fs = new FileStream(path, FileMode.Create)) { encoder.Save(fs); }

            MessageBox.Show("Guardado en escritorio.");
            this.Close();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            SelectionRect.Rect = new Rect(0, 0, 0, 0);
            SelectionBorder.Visibility = CornerTopLeft.Visibility = CornerTopRight.Visibility =
            CornerBottomLeft.Visibility = CornerBottomRight.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Manejadores Visuales
        private void HandleThumb(object sender, DragDeltaEventArgs e)
        {
            Rect r = SelectionRect.Rect;
            double l = r.X, t = r.Y, ri = r.Right, b = r.Bottom;

            if (sender == CornerTopLeft) { l += e.HorizontalChange; t += e.VerticalChange; }
            else if (sender == CornerTopRight) { ri += e.HorizontalChange; t += e.VerticalChange; }
            else if (sender == CornerBottomLeft) { l += e.HorizontalChange; b += e.VerticalChange; }
            else if (sender == CornerBottomRight) { ri += e.HorizontalChange; b += e.VerticalChange; }

            l = Math.Clamp(l, 0, ri - 10); t = Math.Clamp(t, 0, b - 10);
            ri = Math.Clamp(ri, l + 10, ImgTarget.ActualWidth); b = Math.Clamp(b, t + 10, ImgTarget.ActualHeight);
            ActualizarVisuales(new Rect(new Point(l, t), new Point(ri, b)));
        }

        private void ActualizarVisuales(Rect rect)
        {
            SelectionRect.Rect = rect;
            Canvas.SetLeft(SelectionBorder, rect.X); Canvas.SetTop(SelectionBorder, rect.Y);
            SelectionBorder.Width = rect.Width; SelectionBorder.Height = rect.Height;
            PosicionarThumb(CornerTopLeft, rect.Left, rect.Top);
            PosicionarThumb(CornerTopRight, rect.Right, rect.Top);
            PosicionarThumb(CornerBottomLeft, rect.Left, rect.Bottom);
            PosicionarThumb(CornerBottomRight, rect.Right, rect.Bottom);
            CornerTopLeft.Visibility = CornerTopRight.Visibility = CornerBottomLeft.Visibility = CornerBottomRight.Visibility = Visibility.Visible;
        }

        private void PosicionarThumb(Thumb t, double x, double y) { Canvas.SetLeft(t, x - 6); Canvas.SetTop(t, y - 6); }
        #endregion
    }
}