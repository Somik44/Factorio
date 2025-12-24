using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Factorio
{
    public class InventorySlot
    {
        public ResourceType Type { get; set; }
        public int Count { get; set; }
        public Image Icon { get; set; }
        public TextBlock CountText { get; set; }
    }
}
