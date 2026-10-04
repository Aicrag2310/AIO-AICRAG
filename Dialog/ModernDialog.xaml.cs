using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AIO_Aicrag.Dialog
{
    public enum DialogType
    {
        Information,
        Success,
        Warning,
        Error,
        Confirmation
    }

    public partial class ModernDialog : UserControl
    {
        private TaskCompletionSource<bool>? _taskCompletionSource;


        public ModernDialog()
        {
            InitializeComponent();
        }


        // =========================================================
        // MOSTRAR DIALOG
        // =========================================================

        public static Task ShowAsync(
            Window owner,
            string title,
            string message,
            DialogType type = DialogType.Information)
        {
            var dialog = new ModernDialog();

            dialog.Configure(
                title,
                message,
                type,
                false);

            return dialog.ShowInternalAsync(owner);
        }


        // =========================================================
        // CONFIRMAR
        // =========================================================

        public static Task<bool> ConfirmAsync(
            Window owner,
            string title,
            string message)
        {
            var dialog = new ModernDialog();

            dialog.Configure(
                title,
                message,
                DialogType.Confirmation,
                true);

            return dialog.ShowConfirmationInternalAsync(owner);
        }


        // =========================================================
        // CONFIGURAR
        // =========================================================

        private void Configure(
            string title,
            string message,
            DialogType type,
            bool confirmation)
        {
            TxtTitle.Text = title;

            TxtMessage.Text = message;


            BtnSecondary.Visibility =
                confirmation
                    ? Visibility.Visible
                    : Visibility.Collapsed;


            BtnPrimary.Content =
                confirmation
                    ? "Sí, continuar"
                    : "Aceptar";


            switch (type)
            {
                case DialogType.Information:

                    TxtIcon.Text = "i";

                    IconContainer.Background =
                        new SolidColorBrush(
                            Color.FromRgb(2, 132, 199));

                    BtnPrimary.Style =
                        (Style)FindResource(
                            "DialogPrimaryButton");

                    break;


                case DialogType.Success:

                    TxtIcon.Text = "✓";

                    IconContainer.Background =
                        new SolidColorBrush(
                            Color.FromRgb(22, 163, 74));

                    BtnPrimary.Style =
                        (Style)FindResource(
                            "DialogPrimaryButton");

                    break;


                case DialogType.Warning:

                    TxtIcon.Text = "!";

                    IconContainer.Background =
                        new SolidColorBrush(
                            Color.FromRgb(217, 119, 6));

                    BtnPrimary.Style =
                        (Style)FindResource(
                            "DialogPrimaryButton");

                    break;


                case DialogType.Error:

                    TxtIcon.Text = "×";

                    IconContainer.Background =
                        new SolidColorBrush(
                            Color.FromRgb(220, 38, 38));

                    BtnPrimary.Style =
                        (Style)FindResource(
                            "DialogDangerButton");

                    break;


                case DialogType.Confirmation:

                    TxtIcon.Text = "?";

                    IconContainer.Background =
                        new SolidColorBrush(
                            Color.FromRgb(37, 99, 235));

                    BtnPrimary.Style =
                        (Style)FindResource(
                            "DialogPrimaryButton");

                    break;
            }
        }


        // =========================================================
        // DIALOG NORMAL
        // =========================================================

        private async Task ShowInternalAsync(Window owner)
        {
            _taskCompletionSource =
                new TaskCompletionSource<bool>();


            Window dialogWindow =
                CreateDialogWindow(owner);


            dialogWindow.Content = this;


            dialogWindow.ShowDialog();


            await _taskCompletionSource.Task;
        }


        // =========================================================
        // DIALOG CONFIRMACIÓN
        // =========================================================

        private async Task<bool> ShowConfirmationInternalAsync(
            Window owner)
        {
            _taskCompletionSource =
                new TaskCompletionSource<bool>();


            Window dialogWindow =
                CreateDialogWindow(owner);


            dialogWindow.Content = this;


            dialogWindow.ShowDialog();


            return await _taskCompletionSource.Task;
        }


        // =========================================================
        // CREAR WINDOW
        // =========================================================

        private Window CreateDialogWindow(
            Window owner)
        {
            var window = new Window
            {
                Owner = owner,

                WindowStyle =
                    WindowStyle.None,

                ResizeMode =
                    ResizeMode.NoResize,

                ShowInTaskbar =
                    false,

                AllowsTransparency =
                    true,

                Background =
                    Brushes.Transparent,

                Width =
                    owner.ActualWidth,

                Height =
                    owner.ActualHeight,

                Left =
                    owner.Left,

                Top =
                    owner.Top,

                WindowStartupLocation =
                    WindowStartupLocation.Manual
            };


            window.Closed +=
                (sender, args) =>
                {
                    if (_taskCompletionSource != null &&
                        !_taskCompletionSource.Task.IsCompleted)
                    {
                        _taskCompletionSource.TrySetResult(
                            false);
                    }
                };


            return window;
        }


        // =========================================================
        // ACEPTAR
        // =========================================================

        private void BtnPrimary_Click(
            object sender,
            RoutedEventArgs e)
        {
            _taskCompletionSource?
                .TrySetResult(true);


            CloseDialog();
        }


        // =========================================================
        // CANCELAR
        // =========================================================

        private void BtnSecondary_Click(
            object sender,
            RoutedEventArgs e)
        {
            _taskCompletionSource?
                .TrySetResult(false);


            CloseDialog();
        }


        // =========================================================
        // CERRAR
        // =========================================================

        private void CloseDialog()
        {
            Window.GetWindow(this)?.Close();
        }
    }
}