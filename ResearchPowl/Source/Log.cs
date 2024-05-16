// Copyright Karel Kroeze, 2018-2020

namespace ResearchPowl
{
    public static class Log
    {
        public static void Message( string msg, params object[] args )
        {
            Verse.Log.Message(Format(msg, args));
        }

        static string Format( string msg, params object[] args )
        {
            return "[ResearchPowl] " + string.Format(msg, args);
        }

        public static void Error( string msg, bool once, params object[] args )
        {
            var _msg = Format(msg, args);
            if (once) Verse.Log.ErrorOnce(_msg, _msg.GetHashCode());
            else Verse.Log.Error(_msg);
        }

        public static void Debug( string msg, params object[] args )
        {
            if (!ModSettings_ResearchPowl.verboseDebug) return;
            Verse.Log.Message(Format(msg, args));
        }
    }
}
