using System.Windows;

namespace KeySpammer;

public partial class TextInputWindow : Window
{
    public string ResultText { get; private set; } = "";

    public TextInputWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }
    
    public void SetText(string text)
    {
        InputTextBox.Text = text;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        ResultText = InputTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

}