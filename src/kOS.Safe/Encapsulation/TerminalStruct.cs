﻿using System.Collections.Generic;
using kOS.Safe.Encapsulation.Suffixes;
using kOS.Safe.Screen;

namespace kOS.Safe.Encapsulation
{
    [kOS.Safe.Utilities.KOSNomenclature("Terminal")]
    public class TerminalStruct : Structure, IFixedUpdateObserver
    {
        private readonly SafeSharedObjects shared;

        // Some sanity values to prevent the terminal display from getting garbled up:
        // They may have to change after experimentation.
        protected const int MINROWS = 3;

        protected const int MAXROWS = 160;
        protected const int MINCOLUMNS = 15;
        protected const int MAXCOLUMNS = 255;
        
        protected const int MINCHARPIXELS = 4;
        protected const int MAXCHARPIXELS = 24;

        // TODO: To implement IsOpen, we'd have to make a kOS.Safe interface wrapper around TermWindow first.
        // That's more than I want to do in this update, I'm leaving it as a TODO for me or someone else:
        //
        // protected bool IsOpen { get { return shared.Window.IsOpen(); } set {if (value) shared.Window.Open(); else shared.Window.Close(); } }
        
        /// <summary>
        /// This is what we expose to the user script that the user can manipulate to their heart's content.
        /// </summary>
        protected UniqueSetValue<UserDelegate> resizeWatchers;
        
        protected Queue<TriggerInfo> pendingResizeTriggers;
        
        TriggerInfo currentResizeTrigger;

        public TerminalStruct(SafeSharedObjects shared)
        {
            this.shared = shared;
            resizeWatchers = new UniqueSetValue<UserDelegate>();
            pendingResizeTriggers = new Queue<TriggerInfo>();

            InitializeSuffixes();
            
            Shared.Screen.AddResizeNotifier(NotifyMeOfResize);
            if (Shared.UpdateHandler != null) Shared.UpdateHandler.AddFixedObserver(this);
        }
        
        public void Dispose()
        {
            Shared.UpdateHandler.RemoveFixedObserver(this);
            Shared.Screen.RemoveResizeNotifier(NotifyMeOfResize);
        }
        
        public int NotifyMeOfResize(IScreenBuffer sb)
        {
            foreach(UserDelegate watcher in resizeWatchers)
            {
                if (!(watcher is DoNothingDelegate))
                {
                    List<Structure> argList = new List<Structure>(new Structure[] {(ScalarIntValue)sb.ColumnCount, (ScalarIntValue)sb.RowCount});
                    
                    // Normally when you call Shared.Cpu.AddTrigger(), it not only constructs a TriggerInfo, but it
                    // also immediately inserts it into the execution list to start firing off right away.  We want
                    // to delay that, so here's an alternate way to construct a TriggerInfo that isn't running yet,
                    // and isn't attached to the ProgramContext yet, until we choose to add it ourselves later:
                    TriggerInfo notYetExecutingTrigger = new TriggerInfo(watcher.EntryPoint, argList);
                    pendingResizeTriggers.Enqueue(notYetExecutingTrigger);
                }
            }
            return 0; // being told about the resize allows a resizer to choose to scroll the window.  We won't give that power to the script code.
        }
        
        public void KOSFixedUpdate(double deltaTime)
        {
            // Execute just one hook at a time per update, to keep it sane and to keep the
            // multiple Shared.Screen.AddResizeNotifier firings that happen per fixedupdate
            // from resulting in the same hook stepping on top of itself:  This means it may
            // take a few fixedupdates to finish processing all the fired off events, but
            // it's less messy to track than the alternative.
            
            // Only schedule the call to the next one if the previous one isn't still waiting:
            if (currentResizeTrigger == null || currentResizeTrigger.CallbackFinished)
            {
                currentResizeTrigger = pendingResizeTriggers.Dequeue();
                Shared.Cpu.AddTrigger(currentResizeTrigger);
            }
        }

        protected internal SafeSharedObjects Shared
        {
            get { return shared; }
        }

        private void InitializeSuffixes()
        {
            // TODO: Uncomment the following if IsOpen gets implemented later:
            // AddSuffix("ISOPEN", new SetSuffix<BooleanValue>(() => IsOpen, Isopen = value, "true=open, false=closed.  You can set it to open/close the window."));
            AddSuffix("HEIGHT", new ClampSetSuffix<ScalarValue>(() => Shared.Screen.RowCount,
                                                         value => Shared.Screen.SetSize(value, Shared.Screen.ColumnCount),
                                                         MINROWS,
                                                         MAXROWS,
                                                         "Get or Set the number of rows on the screen.  Value is limited to the range [" + MINROWS + "," + MAXROWS + "]"));
            AddSuffix("WIDTH", new ClampSetSuffix<ScalarValue>(() => Shared.Screen.ColumnCount,
                                                        value => Shared.Screen.SetSize(Shared.Screen.RowCount, value),
                                                        MINCOLUMNS,
                                                        MAXCOLUMNS,
                                                        "Get or Set the number of columns on the screen.  Value is limited to the range [" + MINCOLUMNS + "," + MAXCOLUMNS + "]"));
            AddSuffix("REVERSE", new SetSuffix<BooleanValue>(() => Shared.Screen.ReverseScreen,
                                                     value => Shared.Screen.ReverseScreen = value,
                                                     "Get or set the value of whether or not the terminal is in reversed mode."));
            AddSuffix("VISUALBEEP", new SetSuffix<BooleanValue>(() => Shared.Screen.VisualBeep,
                                                       value => Shared.Screen.VisualBeep = value,
                                                       "Get or set the value of whether or not the terminal shows beeps silently with a visual flash."));
            AddSuffix("BRIGHTNESS", new ClampSetSuffix<ScalarValue>(() => Shared.Screen.Brightness,
                                                                    value => Shared.Screen.Brightness = (float)value,
                                                                    0f,
                                                                    1f,
                                                                    "Screen Brightness, between 0.0 and 1.0"));
            AddSuffix("CHARWIDTH", new ClampSetSuffix<ScalarValue>(() => Shared.Screen.CharacterPixelWidth,
                                                                   value => Shared.Screen.CharacterPixelWidth = (int)value,
                                                                   MINCHARPIXELS,
                                                                   MAXCHARPIXELS,
                                                                   2,
                                                                   "Character width on in-game terminal screen in pixels"));
            AddSuffix("CHARHEIGHT", new ClampSetSuffix<ScalarValue>(() => Shared.Screen.CharacterPixelHeight,
                                                                    value => Shared.Screen.CharacterPixelHeight = (int)value,
                                                                    MINCHARPIXELS,
                                                                    MAXCHARPIXELS,
                                                                    2,
                                                                    "Character height on in-game terminal screen in pixels"));
            AddSuffix("RESIZEWATCHERS", new NoArgsSuffix<UniqueSetValue<UserDelegate>>(() => resizeWatchers));
        }
        

        public override string ToString()
        {
            return string.Format("{0} Terminal", base.ToString());
        }
    }
}
