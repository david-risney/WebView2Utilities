using System.Collections.Specialized;
using System.Windows.Controls;

namespace wv2util
{
    public class TextComboBox : System.Windows.Controls.ComboBox
    {
        private bool m_ignore = false;
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            if (!m_ignore)
            {
                base.OnSelectionChanged(e);
            }
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            m_ignore = true;
            try
            {
                base.OnItemsChanged(e);
            }
            finally
            {
                m_ignore = false;
            }
        }
    }
}
