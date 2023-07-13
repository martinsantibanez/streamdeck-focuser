using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synkrono.FocusWindow
{
    public class PluginSettings
    {
        public static PluginSettings CreateDefaultSettings()
        {
            PluginSettings instance = new PluginSettings
            {
                RestoreWindow = false,
                FilteredApps = String.Empty,
                ShowName = true,
                ShowPid = false

            };
            return instance;
        }

        [JsonProperty(PropertyName = "restoreWindow")]
        public bool RestoreWindow { get; set; }

        [JsonProperty(PropertyName = "filteredApps")]
        public String FilteredApps { get; set; }

        [JsonProperty(PropertyName = "showName")]
        public bool ShowName { get; set; }

        [JsonProperty(PropertyName = "showPid")]
        public bool ShowPid { get; set; }


    }
}
