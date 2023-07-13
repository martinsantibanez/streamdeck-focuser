using BarRaider.SdTools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synkrono.FocusWindow.Wrappers;

namespace Synkrono.FocusWindow.Backend
{
    internal class UIManager
    {
        #region Private Members
        private static UIManager instance = null;
        private static readonly object objLock = new object();

        private IUIHandler uiHandler = null;
        private int numberOfKeys;

        #endregion

        #region Constructors

        public static UIManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new UIManager();
                    }
                    return instance;
                }
            }
        }

        private UIManager()
        {
        }

        #endregion

        #region Public Methods

        public event EventHandler<UIActionEventArgs> UIActionEvent;

        public bool IsUIReady
        {
            get
            {
                bool isUIReady = UIActionEvent != null && UIActionEvent.GetInvocationList().Length >= numberOfKeys;
                if (!isUIReady)
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"IsUIReady was called but Subscribers: {UIActionEvent?.GetInvocationList()?.Length}/{numberOfKeys}");
                }
                return isUIReady;
            }
        }

        public void Initialize()
        {
            UIActionEvent = null;
        }

        public bool RegisterUIHandler(IUIHandler gameHandler, int numberOfStreamDeckKeys)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"RegisterUIHandler was called starting {gameHandler.GetType()}");
            numberOfKeys = numberOfStreamDeckKeys;
            this.uiHandler = gameHandler;
            if (this.uiHandler == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"RegisterUIHandler was called with null gameHandler");
                return false;
            }

            return true;
        }


        public void SetAllKeys(UIActionSettings actionSettings)
        {
            UIActionEventArgs action = new UIActionEventArgs(new UIActionSettings[] { actionSettings }, true);
            UIActionEvent?.Invoke(this, action);
        }

        public void SendUIAction(UIActionSettings actionSettings)
        {
            UIActionEventArgs action = new UIActionEventArgs(new UIActionSettings[] { actionSettings }, false);
            UIActionEvent?.Invoke(this, action);
        }

        public void SendUIActions(UIActionSettings[] actionsSettings)
        {
            UIActionEventArgs action = new UIActionEventArgs(actionsSettings, false);
            UIActionEvent?.Invoke(this, action);
        }

        public void ClearAllKeys()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"ClearAllKeys");

            SetAllKeys(new UIActionSettings() { Action = UIActions.DrawImage, BackgroundColor = Color.Black });
        }

        public void NotifyKeyPressed(KeyCoordinates coordinates, bool longKeyPressed)
        {
            if (coordinates == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"NotifyKeyPressed was called with null coordinates");
                return;
            }

            Logger.Instance.LogMessage(TracingLevel.INFO, $"NotifyKeyPressed: Row: {coordinates.Row} Column: {coordinates.Column} long: ${longKeyPressed}");

            if (uiHandler == null)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"NotifyKeyPressed was called but gameHandler is null");
                return;
            }

            if (longKeyPressed)
            {
                uiHandler.ProcessLongKeyPressed(coordinates);
            }
            else
            {
                uiHandler.ProcessKeyPressed(coordinates);
            }
        }

        #endregion
    }
}
