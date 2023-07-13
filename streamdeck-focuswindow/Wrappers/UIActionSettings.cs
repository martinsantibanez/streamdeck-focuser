using BarRaider.SdTools;
using FontAwesome.Sharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synkrono.FocusWindow.Backend;

namespace Synkrono.FocusWindow.Wrappers
{
    internal class UIActionSettings
    {
        public KeyCoordinates Coordinates { get; set; }

        public UIActions Action { get; set; }

        public String Title { get; set; }

        public Color? BackgroundColor { get; set; }

        public Image Image { get; set; }

        public IconChar? FontAwesomeIcon { get; set; }

        public UIActionSettings()
        {
            Coordinates = null;
            Action = UIActions.DrawTitle;
            Title = null;
            BackgroundColor = null;
            Image = null;
            FontAwesomeIcon = null;
        }
    }
}
