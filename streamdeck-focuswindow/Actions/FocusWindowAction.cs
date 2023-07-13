using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using BarRaider.SdTools;
using Synkrono.FocusWindow.Util;
using Synkrono.FocusWindow.Backend;
using System.Diagnostics;

namespace Synkrono.FocusWindow.Actions
{
    [PluginActionId("com.valpro.windowfocuser")]
    public class FocusWindowAction : PluginBase
    {
        #region Private Members

        private readonly PluginSettings settings;

        #endregion
        public FocusWindowAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
                this.settings = PluginSettings.CreateDefaultSettings();
            else
                this.settings = payload.Settings.ToObject<PluginSettings>();
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            SaveSettings();
        }

        public override void Dispose()
        {
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public async override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key was pressed");
            await Connection.SwitchProfileAsync("AppFocuser");
            Logger.Instance.LogMessage(TracingLevel.INFO, "Profile switched");

            await WindowFocuserManager.Instance.ShowFocuser(Connection, settings);
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override void OnTick()
        {
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Received settings: {payload.Settings}");
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods


        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        public List<Process> getProcesses()
        {
            var processLoader = new ProcessFinder();
            var processes= processLoader.GetProcessesWithMainWindow();
            return processes;
        }

        private void Connection_OnSendToPlugin(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;
            string prop = payload["property_inspector"]?.ToString().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(prop))
                return;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"{prop} called");
       
        }

        #endregion
    }
}