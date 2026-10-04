using AIO_Aicrag.Dialog;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AIO_Aicrag
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<AppItem> _allApps = new();

        private CancellationTokenSource? _installationCts;

        private Process? _currentProcess;

        private bool _isInstalling;

        private bool _isDarkTheme;

        private string _currentCategory = "Todas";

        private string _currentSearch = string.Empty;

        /*
         * Debe coincidir con la versión incluida en esta compilación.
         * Ejemplo:
         * 1.0.0 instalada -> release v1.0.1 muestra aviso.
         */
        private const string CurrentVersion = "1.0.0";

        private const string GitHubOwner = "AlejandroG2310";

        /*
         * Cambia este valor por el nombre exacto de tu repositorio.
         */
        private const string GitHubRepository = "AIO_Aicrag";

        private static readonly Uri LatestReleaseUri =
            new Uri(
                $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepository}/releases/latest");

        private static readonly HttpClient UpdateHttpClient =
            CrearHttpClient();

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
        }

        // ============================================================
        // INITIALIZATION
        // ============================================================

        private async void MainWindow_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                AplicarTema(false);

                await CargarAplicacionesAsync();

                InicializarCategorias();

                AplicarFiltros();

                await CargarInformacionHardwareAsync();

                ActualizarEstadoSeleccion();

                await VerificarActualizacionAsync();
            }
            catch (Exception ex)
            {
                RegistrarLog(
                    $"ERROR inicializando aplicación: {ex.Message}");
            }
        }

        // ============================================================
        // UPDATE CHECKER
        // ============================================================

        private static HttpClient CrearHttpClient()
        {
            HttpClient client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "AIO-Aicrag-Updater");

            client.DefaultRequestHeaders.Accept.ParseAdd(
                "application/vnd.github+json");

            return client;
        }

        private async Task VerificarActualizacionAsync()
        {
            try
            {
                using HttpResponseMessage response =
                    await UpdateHttpClient.GetAsync(LatestReleaseUri);

                if (!response.IsSuccessStatusCode)
                {
                    RegistrarLog(
                        $"No se pudo consultar la última versión. " +
                        $"Código HTTP: {(int)response.StatusCode}");

                    return;
                }

                string json =
                    await response.Content.ReadAsStringAsync();

                GitHubRelease? release =
                    JsonSerializer.Deserialize<GitHubRelease>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (release == null)
                    return;

                if (release.Draft || release.Prerelease)
                {
                    RegistrarLog(
                        "La última release no es estable.");

                    return;
                }

                string localVersionText =
                    LimpiarVersion(CurrentVersion);

                string remoteVersionText =
                    LimpiarVersion(release.TagName);

                if (!Version.TryParse(
                        localVersionText,
                        out Version? localVersion))
                {
                    RegistrarLog(
                        $"La versión local no es válida: {CurrentVersion}");

                    return;
                }

                if (!Version.TryParse(
                        remoteVersionText,
                        out Version? remoteVersion))
                {
                    RegistrarLog(
                        $"La versión de GitHub no es válida: " +
                        $"{release.TagName}");

                    return;
                }

                RegistrarLog(
                    $"Versión local: {localVersion}");

                RegistrarLog(
                    $"Última versión disponible: {remoteVersion}");

                if (remoteVersion <= localVersion)
                {
                    RegistrarLog(
                        "La aplicación está actualizada.");

                    return;
                }

                bool openRelease =
                    await ModernDialog.ConfirmAsync(
                        this,
                        $"Hay una nueva versión disponible de AIO Aicrag.\n\n" +
                        $"Versión actual: {localVersion}\n" +
                        $"Nueva versión: {remoteVersion}\n\n" +
                        "¿Quieres abrir la página de descarga?",
                        "Nueva actualización disponible");

                if (openRelease)
                {
                    AbrirUrl(release.HtmlUrl);
                }
            }
            catch (HttpRequestException)
            {
                RegistrarLog(
                    "No fue posible comprobar actualizaciones. " +
                    "Revisa tu conexión a Internet.");
            }
            catch (TaskCanceledException)
            {
                RegistrarLog(
                    "La comprobación de actualizaciones tardó demasiado.");
            }
            catch (Exception ex)
            {
                RegistrarLog(
                    $"ERROR comprobando actualizaciones: {ex.Message}");
            }
        }

        private static string LimpiarVersion(
            string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "0.0.0";

            string cleaned =
                version
                    .Trim()
                    .TrimStart('v', 'V')
                    .Split(
                        '-',
                        StringSplitOptions.RemoveEmptyEntries)[0];

            string[] parts =
                cleaned.Split('.');

            List<string> numericParts = new();

            foreach (string part in parts)
            {
                string numericPart =
                    new string(
                        part
                            .TakeWhile(char.IsDigit)
                            .ToArray());

                if (string.IsNullOrWhiteSpace(numericPart))
                    numericPart = "0";

                numericParts.Add(numericPart);
            }

            while (numericParts.Count < 3)
            {
                numericParts.Add("0");
            }

            return string.Join(
                ".",
                numericParts.Take(4));
        }

        private static void AbrirUrl(
            string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
            }
            catch
            {
                // Ignorar error al abrir el navegador.
            }
        }

        // ============================================================
        // LOAD APPS
        // ============================================================

        private async Task CargarAplicacionesAsync()
        {
            try
            {
                string path =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "app.json");

                if (!File.Exists(path))
                {
                    RegistrarLog(
                        "No se encontró app.json.");

                    await ModernDialog.ShowAsync(
                        this,
                        "No se encontró el archivo app.json.",
                        "Archivo no encontrado",
                        DialogType.Error);

                    return;
                }

                string json =
                    await File.ReadAllTextAsync(path);

                List<AppItem>? apps =
                    JsonSerializer.Deserialize<List<AppItem>>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                _allApps.Clear();

                if (apps != null)
                {
                    foreach (AppItem app in apps)
                    {
                        app.IsSelected = false;

                        _allApps.Add(app);
                    }
                }

                TxtTotalApps.Text =
                    _allApps.Count.ToString();

                RegistrarLog(
                    $"Se cargaron {_allApps.Count} aplicaciones.");
            }
            catch (Exception ex)
            {
                RegistrarLog(
                    $"ERROR cargando aplicaciones: {ex.Message}");
            }
        }

        // ============================================================
        // CATEGORIES
        // ============================================================

        private void InicializarCategorias()
        {
            CategoryPanel.Children.Clear();

            CrearBotonCategoria(
                "Todas",
                true);

            IEnumerable<string> categories =
                _allApps
                    .Select(x => x.Category)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x);

            foreach (string category in categories)
            {
                CrearBotonCategoria(
                    category,
                    false);
            }

            ActualizarBotonesCategoria();
        }

        private void CrearBotonCategoria(
            string category,
            bool selected)
        {
            Button button = new Button
            {
                Content = selected
                    ? $"✓  {category}"
                    : category,

                Tag = category,

                Style = (Style)FindResource(
                    "CategoryButton")
            };

            button.Click += Categoria_Click;

            CategoryPanel.Children.Add(button);
        }

        private void Categoria_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            string category =
                button.Tag?.ToString()
                ?? "Todas";

            _currentCategory = category;

            ActualizarBotonesCategoria();

            AplicarFiltros();
        }

        private void ActualizarBotonesCategoria()
        {
            foreach (UIElement element
                     in CategoryPanel.Children)
            {
                if (element is not Button button)
                    continue;

                string category =
                    button.Tag?.ToString()
                    ?? "Todas";

                bool selected =
                    string.Equals(
                        category,
                        _currentCategory,
                        StringComparison.OrdinalIgnoreCase);

                button.Content =
                    selected
                        ? $"✓  {category}"
                        : category;

                if (selected)
                {
                    button.Background =
                        (Brush)FindResource("Accent");

                    button.Foreground =
                        Brushes.White;

                    button.BorderBrush =
                        (Brush)FindResource("Accent");
                }
                else
                {
                    button.Background =
                        (Brush)FindResource("BgItem");

                    button.Foreground =
                        (Brush)FindResource("TextSecondary");

                    button.BorderBrush =
                        (Brush)FindResource("BorderColor");
                }
            }
        }

        // ============================================================
        // FILTER
        // ============================================================

        private void TxtSearch_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            _currentSearch =
                TxtSearch.Text?.Trim()
                ?? string.Empty;

            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            IEnumerable<AppItem> filtered =
                _allApps;

            if (!string.Equals(
                    _currentCategory,
                    "Todas",
                    StringComparison.OrdinalIgnoreCase))
            {
                filtered =
                    filtered.Where(x =>
                        string.Equals(
                            x.Category,
                            _currentCategory,
                            StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(
                    _currentSearch))
            {
                string search =
                    _currentSearch.ToLowerInvariant();

                filtered =
                    filtered.Where(x =>
                        (x.Name ?? "")
                            .ToLowerInvariant()
                            .Contains(search)
                        ||
                        (x.Description ?? "")
                            .ToLowerInvariant()
                            .Contains(search)
                        ||
                        (x.Category ?? "")
                            .ToLowerInvariant()
                            .Contains(search));
            }

            ListCollectionView view =
                new ListCollectionView(
                    filtered.ToList());

            view.GroupDescriptions.Add(
                new PropertyGroupDescription(
                    nameof(AppItem.Category)));

            AppsControl.ItemsSource = view;

            ActualizarEstadoSeleccion();
        }

        // ============================================================
        // SELECT ALL
        // ============================================================

        private void BtnSelectAll_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isInstalling)
                return;

            foreach (AppItem app in _allApps)
            {
                app.IsSelected = true;
            }

            AplicarFiltros();

            RegistrarLog(
                "Todas las aplicaciones fueron seleccionadas.");
        }

        // ============================================================
        // DESELECT ALL
        // ============================================================

        private void BtnDeselectAll_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isInstalling)
                return;

            foreach (AppItem app in _allApps)
            {
                app.IsSelected = false;
            }

            AplicarFiltros();

            RegistrarLog(
                "Se limpió la selección.");
        }

        // ============================================================
        // PRESETS
        // ============================================================

        private void BtnPreset_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isInstalling)
                return;

            if (sender is not Button button)
                return;

            string preset =
                button.Tag?.ToString()
                ?? string.Empty;

            foreach (AppItem app in _allApps)
            {
                app.IsSelected =
                    string.Equals(
                        app.Preset,
                        preset,
                        StringComparison.OrdinalIgnoreCase);
            }

            AplicarFiltros();

            RegistrarLog(
                $"Preset aplicado: {preset}");
        }

        // ============================================================
        // START
        // ============================================================

        private async void BtnStart_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isInstalling)
                return;

            List<AppItem> selectedApps =
                _allApps
                    .Where(x => x.IsSelected)
                    .ToList();

            if (selectedApps.Count == 0)
            {
                await ModernDialog.ShowAsync(
                    this,
                    "Selecciona al menos una aplicación.",
                    "Sin aplicaciones",
                    DialogType.Warning);

                return;
            }

            bool confirm =
                await ModernDialog.ConfirmAsync(
                    this,
                    $"Se instalarán {selectedApps.Count} aplicaciones.\n\n" +
                    "¿Deseas continuar?",
                    "Iniciar instalación");

            if (!confirm)
                return;

            await InstalarAplicacionesAsync(
                selectedApps);
        }

        // ============================================================
        // INSTALL ALL
        // ============================================================

        private async Task InstalarAplicacionesAsync(
            List<AppItem> apps)
        {
            _installationCts =
                new CancellationTokenSource();

            _isInstalling = true;

            SetInstallingState(true);

            ConsolePanel.Visibility =
                Visibility.Visible;

            ProgressInstall.Value = 0;
            TxtProgress.Text = "0%";

            TxtSystemStatus.Text =
                "Instalando";

            TxtSystemStatus.Foreground =
                (Brush)FindResource("Accent");

            TxtLog.Clear();

            int successful = 0;
            int failed = 0;

            RegistrarLog(
                "========================================");

            RegistrarLog(
                "AIO AICRAG - INICIO DE INSTALACIÓN");

            RegistrarLog(
                "========================================");

            try
            {
                for (int i = 0;
                     i < apps.Count;
                     i++)
                {
                    _installationCts.Token
                        .ThrowIfCancellationRequested();

                    AppItem app =
                        apps[i];

                    RegistrarLog("");

                    RegistrarLog(
                        $"[{i + 1}/{apps.Count}] {app.Name}");

                    bool result =
                        await InstalarAplicacionAsync(
                            app,
                            _installationCts.Token);

                    if (result)
                        successful++;
                    else
                        failed++;

                    double progress =
                        ((i + 1d) / apps.Count) * 100d;

                    ProgressInstall.Value =
                        progress;

                    TxtProgress.Text =
                        $"{progress:0}%";
                }
            }
            catch (OperationCanceledException)
            {
                RegistrarLog("");

                RegistrarLog(
                    "Instalación cancelada por el usuario.");
            }
            catch (Exception ex)
            {
                RegistrarLog(
                    $"ERROR GENERAL: {ex.Message}");
            }
            finally
            {
                _currentProcess = null;

                _installationCts.Dispose();

                _installationCts = null;

                _isInstalling = false;

                SetInstallingState(false);

                TxtSystemStatus.Text =
                    "Listo";

                TxtSystemStatus.Foreground =
                    (Brush)FindResource("Success");

                RegistrarLog("");

                RegistrarLog(
                    "========================================");

                RegistrarLog(
                    $"Finalizadas: {successful}");

                RegistrarLog(
                    $"Fallidas: {failed}");

                RegistrarLog(
                    "========================================");

                if (successful > 0 || failed > 0)
                {
                    await ModernDialog.ShowAsync(
                        this,
                        $"Instalación terminada.\n\n" +
                        $"Exitosas: {successful}\n" +
                        $"Fallidas: {failed}",
                        "Proceso terminado",
                        failed == 0
                            ? DialogType.Success
                            : DialogType.Warning);
                }

                PreguntarAccionFinal();
            }
        }

        // ============================================================
        // INSTALL APP
        // ============================================================

        private async Task<bool> InstalarAplicacionAsync(
            AppItem app,
            CancellationToken cancellationToken)
        {
            try
            {
                if (string.Equals(
                        app.Type,
                        "winget",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return await InstalarConWinGetAsync(
                        app,
                        cancellationToken);
                }

                RegistrarLog(
                    $"Tipo de instalación no soportado: {app.Type}");

                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RegistrarLog(
                    $"ERROR instalando {app.Name}: {ex.Message}");

                return false;
            }
        }

        // ============================================================
        // WINGET
        // ============================================================

        private async Task<bool> InstalarConWinGetAsync(
            AppItem app,
            CancellationToken cancellationToken)
        {
            RegistrarLog(
                $"Ejecutando WinGet para {app.Name}");

            ProcessStartInfo psi =
                new ProcessStartInfo
                {
                    FileName = "winget",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory =
                        AppDomain.CurrentDomain.BaseDirectory
                };

            psi.ArgumentList.Add("install");
            psi.ArgumentList.Add("--id");
            psi.ArgumentList.Add(app.PathOrId);
            psi.ArgumentList.Add("--exact");

            if (!string.IsNullOrWhiteSpace(
                    app.Arguments))
            {
                string[] extraArguments =
                    app.Arguments
                        .Split(
                            ' ',
                            StringSplitOptions.RemoveEmptyEntries);

                foreach (string argument
                         in extraArguments)
                {
                    psi.ArgumentList.Add(argument);
                }
            }

            psi.ArgumentList.Add(
                "--accept-package-agreements");

            psi.ArgumentList.Add(
                "--accept-source-agreements");

            using Process process =
                new Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

            _currentProcess = process;

            process.OutputDataReceived +=
                Process_OutputDataReceived;

            process.ErrorDataReceived +=
                Process_ErrorDataReceived;

            RegistrarLog(
                $"Comando: winget install --id " +
                $"{app.PathOrId} --exact");

            if (!process.Start())
            {
                RegistrarLog(
                    $"No se pudo iniciar WinGet para {app.Name}");

                return false;
            }

            process.BeginOutputReadLine();

            process.BeginErrorReadLine();

            await process.WaitForExitAsync(
                cancellationToken);

            int exitCode =
                process.ExitCode;

            if (exitCode == 0)
            {
                RegistrarLog(
                    $"✓ {app.Name} instalada correctamente.");

                return true;
            }

            RegistrarLog(
                $"✗ {app.Name} terminó con código {exitCode}.");

            return false;
        }

        // ============================================================
        // PROCESS OUTPUT
        // ============================================================

        private void Process_OutputDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                RegistrarLog(e.Data);
            }
        }

        private void Process_ErrorDataReceived(
            object sender,
            DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                RegistrarLog(
                    $"ERROR: {e.Data}");
            }
        }

        // ============================================================
        // CANCEL
        // ============================================================

        private void BtnCancel_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_isInstalling)
                return;

            RegistrarLog(
                "Solicitando cancelación...");

            _installationCts?.Cancel();

            try
            {
                if (_currentProcess != null &&
                    !_currentProcess.HasExited)
                {
                    _currentProcess.Kill(true);
                }
            }
            catch
            {
                // Ignorar error al cerrar proceso.
            }
        }

        // ============================================================
        // INSTALLING STATE
        // ============================================================

        private void SetInstallingState(
            bool installing)
        {
            BtnStart.IsEnabled =
                !installing;

            BtnCancel.IsEnabled =
                installing;

            BtnSelectAll.IsEnabled =
                !installing;

            BtnDeselectAll.IsEnabled =
                !installing;

            TxtSearch.IsEnabled =
                !installing;

            foreach (UIElement element
                     in CategoryPanel.Children)
            {
                element.IsEnabled =
                    !installing;
            }

            BtnPresetBasic.IsEnabled =
                !installing;

            BtnPresetOffice.IsEnabled =
                !installing;

            BtnPresetGaming.IsEnabled =
                !installing;
        }

        // ============================================================
        // SELECTION STATUS
        // ============================================================

        private void ActualizarEstadoSeleccion()
        {
            int selected =
                _allApps.Count(
                    x => x.IsSelected);

            TxtSelectedApps.Text =
                selected.ToString();

            TxtStatus.Text =
                selected == 0
                    ? "0 aplicaciones seleccionadas"
                    : $"{selected} aplicaciones seleccionadas";

            if (!_isInstalling)
            {
                TxtSystemStatus.Text =
                    selected > 0
                        ? "Listo para instalar"
                        : "Listo";

                TxtSystemStatus.Foreground =
                    (Brush)FindResource("Success");
            }
        }

        // ============================================================
        // HARDWARE
        // ============================================================

        private async Task CargarInformacionHardwareAsync()
        {
            try
            {
                string result =
                    await Task.Run(() =>
                    {
                        int cores =
                            Environment.ProcessorCount;

                        long ramBytes =
                            GC.GetGCMemoryInfo()
                              .TotalAvailableMemoryBytes;

                        double ramGb =
                            ramBytes /
                            1024d /
                            1024d /
                            1024d;

                        string systemDrive =
                            Path.GetPathRoot(
                                Environment.SystemDirectory)
                            ?? "C:\\";

                        DriveInfo drive =
                            new DriveInfo(
                                systemDrive);

                        double totalGb =
                            drive.TotalSize /
                            1024d /
                            1024d /
                            1024d;

                        double freeGb =
                            drive.AvailableFreeSpace /
                            1024d /
                            1024d /
                            1024d;

                        return
                            $"{cores} núcleos  •  " +
                            $"RAM {ramGb:0} GB  •  " +
                            $"{freeGb:0}/{totalGb:0} GB libres";
                    });

                TxtInfoHardware.Text =
                    result;
            }
            catch
            {
                TxtInfoHardware.Text =
                    "Hardware no disponible.";
            }
        }

        // ============================================================
        // THEME
        // ============================================================

        private void BtnToggleTheme_Click(
            object sender,
            RoutedEventArgs e)
        {
            _isDarkTheme =
                !_isDarkTheme;

            AplicarTema(
                _isDarkTheme);
        }

        private void AplicarTema(
            bool dark)
        {
            if (dark)
            {
                SetBrush("BgWindow", "#0B0F14");
                SetBrush("BgSidebar", "#10151C");
                SetBrush("BgCard", "#151B23");
                SetBrush("BgItem", "#1B222C");
                SetBrush("BgHover", "#232C38");

                SetBrush("TextPrimary", "#F8FAFC");
                SetBrush("TextSecondary", "#A8B0C0");
                SetBrush("TextMuted", "#6F788A");

                SetBrush("BorderColor", "#27303C");

                SetBrush("Accent", "#3B82F6");
                SetBrush("AccentHover", "#2563EB");
                SetBrush("AccentLight", "#172D4D");

                SetBrush("AccentRed", "#DC2626");
                SetBrush("AccentRedHover", "#B91C1C");

                SetBrush("Success", "#22C55E");
                SetBrush("Warning", "#F59E0B");
                SetBrush("Danger", "#EF4444");

                SetBrush("ConsoleBg", "#070A0F");
                SetBrush("ConsoleText", "#9FE870");

                TxtThemeButton.Text =
                    "Modo claro";
            }
            else
            {
                SetBrush("BgWindow", "#F5F7FB");
                SetBrush("BgSidebar", "#FFFFFF");
                SetBrush("BgCard", "#FFFFFF");
                SetBrush("BgItem", "#F1F4F9");
                SetBrush("BgHover", "#E8EEF9");

                SetBrush("TextPrimary", "#172033");
                SetBrush("TextSecondary", "#667085");
                SetBrush("TextMuted", "#98A2B3");

                SetBrush("BorderColor", "#E2E7F0");

                SetBrush("Accent", "#2563EB");
                SetBrush("AccentHover", "#1D4ED8");
                SetBrush("AccentLight", "#E8F0FF");

                SetBrush("AccentRed", "#DC2626");
                SetBrush("AccentRedHover", "#B91C1C");

                SetBrush("Success", "#16A34A");
                SetBrush("Warning", "#D97706");
                SetBrush("Danger", "#DC2626");

                SetBrush("ConsoleBg", "#101828");
                SetBrush("ConsoleText", "#7DD3FC");

                TxtThemeButton.Text =
                    "Modo oscuro";
            }

            ActualizarBotonesCategoria();
        }

        private void SetBrush(
            string key,
            string hex)
        {
            Color color =
                (Color)ColorConverter
                    .ConvertFromString(hex)!;

            Resources[key] =
                new SolidColorBrush(color);
        }

        // ============================================================
        // CONSOLE
        // ============================================================

        private void RegistrarLog(
            string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() =>
                    RegistrarLog(message));

                return;
            }

            string line =
                $"[{DateTime.Now:HH:mm:ss}] {message}";

            TxtLog.AppendText(
                line +
                Environment.NewLine);

            TxtLog.ScrollToEnd();
        }

        private void BtnCloseConsole_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_isInstalling)
                return;

            ConsolePanel.Visibility =
                Visibility.Collapsed;
        }

        // ============================================================
        // FINAL ACTION
        // ============================================================

        private void PreguntarAccionFinal()
        {
            if (ChkRestart != null &&
                ChkRestart.IsChecked == true)
            {
                EjecutarShutdown(
                    "/r /t 5");

                return;
            }

            if (ChkShutdown != null &&
                ChkShutdown.IsChecked == true)
            {
                EjecutarShutdown(
                    "/s /t 5");
            }
        }

        private void EjecutarShutdown(
            string arguments)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "shutdown.exe",
                        Arguments = arguments,
                        UseShellExecute = true
                    });
            }
            catch (Exception ex)
            {
                RegistrarLog(
                    $"No se pudo ejecutar acción final: {ex.Message}");
            }
        }

        // ============================================================
        // SIDEBAR
        // ============================================================

        private void BtnHome_Click(
            object sender,
            RoutedEventArgs e)
        {
            _currentCategory =
                "Todas";

            _currentSearch =
                string.Empty;

            TxtSearch.Text =
                string.Empty;

            ActualizarBotonesCategoria();

            AplicarFiltros();
        }

        private void BtnApplications_Click(
            object sender,
            RoutedEventArgs e)
        {
            _currentCategory =
                "Todas";

            ActualizarBotonesCategoria();

            AplicarFiltros();
        }

        private void BtnPresets_Click(
            object sender,
            RoutedEventArgs e)
        {
            BtnPresetBasic_Click(
                sender,
                e);
        }

        private void BtnPresetBasic_Click(
            object sender,
            RoutedEventArgs e)
        {
            foreach (AppItem app in _allApps)
            {
                app.IsSelected =
                    string.Equals(
                        app.Preset,
                        "Basico",
                        StringComparison.OrdinalIgnoreCase);
            }

            AplicarFiltros();
        }
    }

    // ================================================================
    // GITHUB RELEASE MODEL
    // ================================================================

    public sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }

    // ================================================================
    // APP MODEL
    // ================================================================

    public class AppItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public string Description
        {
            get;
            set;
        } = string.Empty;

        public string Category
        {
            get;
            set;
        } = string.Empty;

        public string Preset
        {
            get;
            set;
        } = string.Empty;

        public string Type
        {
            get;
            set;
        } = "winget";

        public string PathOrId
        {
            get;
            set;
        } = string.Empty;

        public string Arguments
        {
            get;
            set;
        } = string.Empty;

        public string Icon
        {
            get;
            set;
        } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;

            set
            {
                if (_isSelected == value)
                    return;

                _isSelected =
                    value;

                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(
                        nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;
    }
}