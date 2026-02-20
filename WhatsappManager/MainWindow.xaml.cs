using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace WhatsappManager
{


    public partial class MainWindow : Window
    {
        private string instanceId;
        private string token;

        public ObservableCollection<Contacto> MisContactos { get; set; }

        public MainWindow()
        {
            MisContactos = new ObservableCollection<Contacto>();
            InitializeComponent();
            instanceId = "instance162272";
            token = "ivce5k8c1bkjaa39";
            CargarContactosPrueba();
        }

        private void CargarContactosPrueba()
        {
            CargarContactosDesdeUltraMsg().ConfigureAwait(true);
            lbContactos.ItemsSource = MisContactos;
        }


        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            var seleccionado = lbContactos.SelectedItem as Contacto;
            if (seleccionado == null) return;

            // Llamada a UltraMsg (Usa el código HttpClient que vimos antes)
            bool exito = await EnviarUltraMsg(seleccionado.Numero, txtMensaje.Text);

            if (exito)
            {
                seleccionado.UltimoMensaje = "Tú: " + txtMensaje.Text;
                lbContactos.Items.Refresh(); // Actualiza la lista lateral
                txtMensaje.Clear();
            }
        }
        private async void lbContactos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbContactos.SelectedItem is Contacto seleccionado)
            {
                gridChat.Visibility = Visibility.Visible;
                lblChatNombre.Text = seleccionado.Nombre;

                // Limpiamos el panel antes de cargar los nuevos mensajes
                panelMensajes.Children.Clear();

                await CargarHistorialChat(seleccionado.Numero);
            }
        }
        private void AgregarMensajeAlUI(string texto, bool esMio, string autor = "")
        {
            StackPanel container = new StackPanel
            {
                HorizontalAlignment = esMio ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(10, 2, 10, 2)
            };

            // Si es grupo y no soy yo, ponemos el número del autor en pequeñito
            if (!esMio && !string.IsNullOrEmpty(autor))
            {
                container.Children.Add(new TextBlock
                {
                    Text = autor.Split('@')[0],
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(5, 0, 0, 0)
                });
            }

            Border burbuja = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 5, 10, 5),
                Background = (Brush)new BrushConverter().ConvertFrom(esMio ? "#D9FDD3" : "#FFFFFF"),
                MaxWidth = 500
            };

            burbuja.Child = new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap };
            container.Children.Add(burbuja);
            panelMensajes.Children.Add(container);
        }
        private async Task CargarHistorialChat(string idBase)
        {
            // El chatId debe tener el formato numero@c.us
            string idLimpio = idBase.Split('@')[0];

            // Determinamos el sufijo: 
            // Los IDs de grupos suelen ser más largos o contener un guion '-'
            string sufijo = idLimpio.Contains("-") || idLimpio.Length > 15 ? "@g.us" : "@c.us";

            string chatId = idLimpio + sufijo;
            string url = $"https://api.ultramsg.com/{instanceId}/chats/messages?token={token}&chatId={chatId}&limit=50";

            string miNumero = "5217296333276"; // Tu número emisor
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var listaMensajes = JsonConvert.DeserializeObject<List<UltraMsgMessage>>(json);

                        if (listaMensajes != null)
                        {
                            panelMensajes.Children.Clear();
                            // Ordenamos: el más antiguo arriba


                            foreach (var msg in listaMensajes)
                            {
                                // Usamos 'fromMe' directamente del JSON
                                bool esMio = msg.fromMe;

                                switch (msg.type)
                                {
                                    case "chat":
                                        if (!string.IsNullOrEmpty(msg.body))
                                            AgregarMensajeAlUI(msg.body, esMio, msg.author);
                                        break;

                                    case "image":
                                        // VALIDACIÓN CRÍTICA: Solo si el body parece una URL
                                        if (!string.IsNullOrEmpty(msg.body) && msg.body.StartsWith("http"))
                                            AgregarImagenAlUI(msg.body, esMio);
                                        else
                                            AgregarMensajeAlUI("📷 [Imagen]", esMio, msg.author);
                                        break;

                                    case "sticker":
                                        AgregarMensajeAlUI("🎨 [Sticker]", esMio, msg.author);
                                        break;

                                    default:
                                        AgregarMensajeAlUI($"[{msg.type}]", esMio, msg.author);
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar mensajes: " + ex.Message);
            }
        }
        private void AgregarImagenAlUI(string urlImagen, bool esMio)
        {
            StackPanel container = new StackPanel
            {
                HorizontalAlignment = esMio ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                Margin = new Thickness(10, 5, 10, 5)
            };

            Border burbuja = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5),
                Background = (Brush)new BrushConverter().ConvertFrom(esMio ? "#D9FDD3" : "#FFFFFF"),
                MaxWidth = 300
            };

            Image foto = new Image
            {
                Source = new BitmapImage(new Uri(urlImagen)),
                Stretch = Stretch.Uniform,
                MaxWidth = 280
            };

            burbuja.Child = foto;
            container.Children.Add(burbuja);
            panelMensajes.Children.Add(container);
            scrollChat.ScrollToEnd();
        }
        private async Task<bool> EnviarUltraMsg(string numero, object text)
        {
            throw new NotImplementedException();
        }
        private async Task CargarContactosDesdeUltraMsg()
        {


            // Endpoint para obtener la lista de chats actuales
            string url = $"https://api.ultramsg.com/{instanceId}/chats?token={token}";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var chatsApi = JsonConvert.DeserializeObject<List<UltraMsgChat>>(json);

                        MisContactos?.Clear();

                        foreach (var chat in chatsApi)
                        {
                            // Convertir Timestamp de Unix a hora legible
                            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                            dtDateTime = dtDateTime.AddSeconds(chat.timestamp).ToLocalTime();

                            MisContactos.Add(new Contacto
                            {
                                Nombre = string.IsNullOrEmpty(chat.name) ? chat.id.Split('@')[0] : chat.name,
                                Numero = chat.id.Split('@')[0],
                                UltimoMensaje = chat.last_message,
                                Hora = dtDateTime.ToString("HH:mm")
                            });
                        }
                    }
                    else
                    {
                        MessageBox.Show("No se pudo conectar con UltraMsg. Revisa tu Token e Instance ID.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar contactos: {ex.Message}");
            }
        }
    }
    public class Contacto
    {
        public string Nombre { get; set; }
        public string Numero { get; set; }
        public string UltimoMensaje { get; set; }
        public string Hora { get; set; }
        public string FotoUrl { get; set; } // Opcional para la imagen de perfil
    }
    public class UltraMsgChat
    {
        public string id { get; set; }      // Ejemplo: "521234567890@c.us"
        public string name { get; set; }    // Nombre del contacto
        public string last_message { get; set; }
        public long timestamp { get; set; } // Fecha en formato Unix
    }
    public class UltraMsgResponse
    {
        public List<UltraMsgMessage> messages { get; set; }
    }

    public class UltraMsgMessage
    {
        public string from { get; set; }
        public string body { get; set; }
        public bool fromMe { get; set; } // Cambiado para coincidir con el JSON
        public string type { get; set; }
        public string author { get; set; } // En grupos, 'author' es quien escribió
    }
}