using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KeyLine.UI.MainWindow.Controls;

public partial class ProfileDropdownBlock : UserControl
{
    public ProfileDropdownBlock()
    {
        InitializeComponent();
    }

    public Button ProfileButton => ProfileSelectorButton;
    public TextBlock SelectedProfileText => SelectedProfileTextBlock;
    public Popup ProfilesPopup => ProfilePopup;
    public ScrollViewer ProfilesScrollViewer => ProfileScrollViewer;
    public StackPanel ProfilesPanel => ProfileRowsPanel;
    public Canvas ProfilesDragOverlay => ProfileDragOverlay;
    public Button AddProfileButtonControl => AddProfileButton;
}
