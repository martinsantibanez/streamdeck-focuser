using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synkrono.FocusWindow.Wrappers
{
    internal class UIActionEventArgs
    {
        public UIActionSettings[] Settings { get; set; }
        public bool AllKeysAction { get; set; }


        public UIActionEventArgs(UIActionSettings[] uiActionSettings, bool allKeysAction = false)
        {
            Settings = uiActionSettings;
            AllKeysAction = allKeysAction;
        }
    }
}
