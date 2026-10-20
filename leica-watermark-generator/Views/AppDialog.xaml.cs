using System.Windows;
using System.Windows.Input;

namespace leica_watermark_generator.Views
{
    /// <summary>
    /// 替代系统 MessageBox 的暗色对话框，风格和主界面一致。
    /// </summary>
    public partial class AppDialog : Window
    {
        private AppDialog()
        {
            InitializeComponent();
        }

        public static bool Show(Window? owner, string title, string message,
            string okText = "确定", bool showCancel = true, bool danger = false)
        {
            var dialog = Create(owner, title, message, okText, showCancel, danger);
            return dialog.ShowDialog() == true;
        }

        internal static AppDialog Create(Window? owner, string title, string message,
            string okText = "确定", bool showCancel = true, bool danger = false)
        {
            var dialog = new AppDialog();
            if (owner != null && owner.IsLoaded)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.TitleText.Text = title;
            dialog.MessageText.Text = message;
            dialog.OkButton.Content = okText;
            dialog.OkButton.Style = (Style)dialog.FindResource(danger ? "Btn.Danger" : "Btn.Primary");
            dialog.CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

            return dialog;
        }

        public static void Info(Window? owner, string title, string message)
            => Show(owner, title, message, "知道了", false);

        public static void Error(Window? owner, string title, string message)
            => Show(owner, title, message, "知道了", false);

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
    }
}
