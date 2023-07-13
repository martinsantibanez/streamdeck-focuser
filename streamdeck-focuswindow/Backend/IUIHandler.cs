using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synkrono.FocusWindow.Backend
{
    public interface IUIHandler
    {
        void ProcessKeyPressed(KeyCoordinates coordinates);
        void ProcessLongKeyPressed(KeyCoordinates coordinates);
    }
}
