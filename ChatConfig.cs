using StreamCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EnhancedStreamChat
{
    public class ChatConfig
    {
        private string _configPath = Path.Combine(Environment.CurrentDirectory, "UserData", $"{Assembly.GetExecutingAssembly().GetName().Name}.ini");

        

        public ChatConfig()
        {
            Load();
        }

        public void Load()
        {
            ObjectSerializer.Load(this, _configPath);
        }

        public void Save()
        {
            ObjectSerializer.Save(this, _configPath);
        }
    }
}
