using System.Windows;

namespace LogScope.App.Views;

public partial class InputDialog : Window
{
    public string ResponseText => ResponseBox.Text;

    public InputDialog(string title, string prompt, string initialValue = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ResponseBox.Text = initialValue;
        Loaded += (_, _) => { ResponseBox.SelectAll(); ResponseBox.Focus(); };
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
