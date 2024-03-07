/*
 * Project Name : Big Ben
 * Created by Grumbo aka slp13at420 aka The Mad Scientist
 * GitHub : https://github.com/BlackWolfsDen/
 * creation start date : 2-29-2024
 * creation finish date : 3-6-2024
 * 
 * Language : C#
 * Platform : Rust Game Server (Oxide Plugin)
 * Version : 1.1.0
 * Purpose : Game Mod-Command/Control
 * 
 * Description : 
 *  This creates custom ServerSide global subscribable eventhooks based on game time Now PLUS by date \o/ Im MAD!!.
 *  want that custom pve plugin to only fire at a single or multiple specific game.times ?
 *  just create a new timer with unique relateable event name and time i.e. railroadstart 09:00.
 *  Designed to eliminate multiple timers that run in mods with just 1 master timer to run all timed mods.
 *  Want your mods to only run during daylight \o/ it can be done !
 *  
 *  Project Repo :  --> https://github.com/BlackWolfsDen/Grumboz_Big_Ben/
*/


using Newtonsoft.Json;

using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Game.Rust.Libraries;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oxide.Plugins
{
    [Info("Grumbo'z Big Ben", "Grumbo", "1.1.0")]
    [Description("Creates Custom Global Subscribable Event Hooks at specific Times of day or days of months ! To automate " +
        "Manual event Plugins or for Centralized control of all your automated Plugins to minimize events accidently starting at the same time.")]

    public class BigBen : RustPlugin
    {
        #region misc variables

        private bool testall = false; // false || true . used for testing all with output to console
        private bool testtick = false; // false || true . used to test ticking.
        private bool testevent = false; // false || true . used to test event circuit.
        private bool testmethod = false; // false || true . used to test event hook method.
        private bool announceevents = false; // false || true . allows BigBen to announce when an event hook fires.
        private int tick = 9; // if using a day/nite time manager plugin you may need to decline system_ticks per check here.
        private int tickcnt = 0; // used to store the value of ticks till it passes a check.
        Dictionary<string, string> dbt = new Dictionary<string, string>(); // used to store the time of a time event when last triggered to avoid double triggers.
        Dictionary<string, string> dbd = new Dictionary<string, string>(); // used to store the time of a date event when last triggered to avoid double triggers.

        #endregion misc variables

        #region Config        

        private EventTimes config;
        
/*
        Theses 2 tables below (Timers , Dates) contains examples for time of day based timers and date based timers.
        time based trigger at the set times of day. requires only set times.
        date triggered only trigger on the set time for set dates. requires only 1 trigger time and 1 or multiple dates .

                        DONT FORGET COMMAS ,! AND USE JSON FILE TO DO EDITS
*/
   
        public class EventTimes
        {
            [JsonProperty("Timers (event name, event time)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> Timers = new Dictionary<string, string>()
            {
                { "Dawn","07:00" },
                { "Noon","12:00" },
                { "Dusk","18:00" },
                { "Midnight","00:00" },
                { "Test","07:30,08:00,09:01" }
            };

            [JsonProperty("Dates (event name, event date/time)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> Dates = new Dictionary<string, string>()
            {
                { "WipeProtectStart","08:00,03/05,03/06" },
            };
            /*
                Above are just pre added examples of event hook name and times.
             */

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

        }

        protected override void LoadDefaultConfig() => config = new EventTimes();
       
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<EventTimes>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion config

        //

        #region Init
        private const string permallow = "bigben.allow";

        private List<string> commands = new List<string> { nameof(EventListCMD) };

        private void OnServerInitialized()
        {
            //register permissions
            permission.RegisterPermission(permallow, this);

            //register commands
            commands.ForEach(command => AddLocalizedCommand(command));

        }
        #endregion Init

        //

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerms"] = "You don't have permission to use this command.",
                ["TimedEventList"] = "Custom Timed Events: {0}",
                ["DateEventList"] = "Custom Date based Events: {0}",
//
                ["EventListCMD"] = "bigbenlist",
            }, this);
        }

        #endregion Localization

        //

        #region Commands

/*
 *      its easier and clearer to edit by the json file rather than using the unfinished command structure
 *      So i may never add ingame commands.
*/

        private void EventListCMD(IPlayer player, string command, string[] args)
        {
            if (!HasPerm(player.Id, permallow))
            {
                Message(player, "NoPerms");
                return;
            }

            Dictionary<string, string> k = config.Timers;
            string msg = string.Empty;
            
            foreach (KeyValuePair<string, string> v in k)
            {
                msg += "" + v.Key + "-[" + v.Value + "]" + ", ";
            }
            Message(player, "TimedEventList", msg);

            k = null;
            k = config.Dates;
            msg = string.Empty;

            foreach (KeyValuePair<string, string> v in k)
            {
                msg += "" + v.Key + "-[" + v.Value + "]" + ", ";
            }
            Message(player, "DateEventList", msg);

        }

        #endregion Commands

        //

        #region Methods

        private void UpdateEvents()
        {
            List<string> listtmr = new List<string>();
            List<string> listdatetmr = new List<string>();

            if (!config.Timers.IsEmpty())
            {
                foreach (var tevent in config.Timers)
                {
                    string tmr = tevent.Key + "," + tevent.Value + ",";

                    if (!listtmr.Contains(tmr))
                        listtmr.Add(tmr);
                }
            }

            if (!config.Dates.IsEmpty())
            {
                foreach (var devent in config.Dates)
                {
                    string datetmr = devent.Key + "," + devent.Value + ",";

                    if (!listdatetmr.Contains(datetmr))
                        listdatetmr.Add(datetmr);
                }
            }

            SaveConfig();
        }

        private void RingEventToPlugins(string msg, bool service)
        {
            if (service)
            {
                msg = "On" + msg; // parses "On" to the start of event name so it is more proffesional looking blahhhhh

                Interface.Oxide.CallHook(msg, msg, true);

                if (testevent || testall) { PrintWarning(msg + " Rang out."); }
            }
        }
        
        #endregion Methods

        // Engine

        #region Oxide Hooks

        void OnTick()
        {
            /* Core Timer
             *  using OnTick to best adapt for accelerated day/night cycle
            */

            if (tickcnt == tick)
            {
                if (testtick) { PrintWarning("DATE LOOP " + TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm")); }

                var realdate = System.DateTime.Now.ToString("MM/dd");
                var realtime = System.DateTime.Now.ToString("HH:mm");

                var datetime = TOD_Sky.Instance.Cycle.DateTime;
                var gametime = datetime.ToString("HH:mm");

                Dictionary<string, string> k = config.Dates; // searching date timers first

                string tmr = string.Empty;
                string key = string.Empty;
                string value = string.Empty;
                int size;
                               
                foreach(KeyValuePair<string, string> v in k) // loop thru date timers parse 1 elemnt to v per loop.
                {
                    key = v.Key;
                    value = v.Value;

                    if (!dbd.ContainsKey(key)) { dbd[key] = ""; } // creates new key entry if needed. Date timers

                    string[] arg = value.Split(','); // arg[0] = time , arg[1++] dates MM/dd

                    size = arg.Length;

                    for (int i = 1; i < size; i++) // break single array row into v. (v.Key = name , v.Value = time,date)
                    {
                        if (arg[0] == realtime) 
                        {
                            if (i >= 1)
                            {
                                if (arg[i] == realdate)
                                {
                                    if (dbd[key] != realdate) // Checks if event name last stored date fails equaling current game date then pass thru.
                                    {
                                        dbd[key] = realdate; // stores new current game time HH:mm to event name of the time check db.

                                        RingEventToPlugins(key, true);

                                        if (announceevents) { PrintWarning("" + key + " : " + arg[i]); }
                                    }
                                }
                            }
                        }
                    }
                }

                k = null; // clearing k
                k = config.Timers; // now searching thru time timers
                value = null;

                if (testtick) { PrintWarning("TIME LOOP " + TOD_Sky.Instance.Cycle.DateTime.ToString("HH:mm")); }

                foreach (KeyValuePair<string, string> v in k) // Loops thru time timers parses 1 elemnt to v per loop
                {
                    key = v.Key;
                    value = v.Value;

                    if (!dbt.ContainsKey(key)) { dbt[key] = ""; }

                    string[] arg = value.Split(',');
                    size = arg.Length;

                   for (int i = 0; i < size; i++)
                    {
                        if (testtick || testall){ PrintWarning(gametime + " : " + key + " : " + arg[i] + " : " + tick); }

                        if (arg[i] == gametime) // checks if event time equals current game time.
                        {
                            if (dbt[key] != arg[i]) // Checks if event name last stored time fails equaling current game time.
                            {
                                dbt[key] = arg[i]; // stores new current game time HH:MM to event name of the time check db.

                                RingEventToPlugins(key, true);
                            
                                if (announceevents) { PrintWarning("" + key + " : " + arg[i]); }
                            }
                        }
                   }
                }

                tickcnt = 0;
            }

            tickcnt++;
        }

        #endregion

        // Responce building to client language

        #region Language

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (!message.Key.Equals(command)) continue;

                    if (string.IsNullOrEmpty(message.Value)) continue;

                    AddCovalenceCommand(message.Value, command);
                }
            }
        }

        #endregion
    }
}
