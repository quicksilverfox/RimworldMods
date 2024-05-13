// WealthDebugger.cs
// Copyright Karel Kroeze, 2017-2021

#if DEGUG
#define DEBUG_WEALTH
#endif


namespace StuffedFloors {
#if DEBUG_WEALTH
    public class WealthDebugger : MapComponent {
        public WealthDebugger( Map map ) : base( map ){}

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            var rect = new Rect( 0f, Screen.height / 4f, Screen.width / 4f, Screen.height / 2f );
            var Wealth = Find.CurrentMap.wealthWatcher;

            string msg = $"Total wealth: {Wealth.WealthTotal}";
            msg += $"\n\tBuildings: {Wealth.WealthBuildings}";
            msg += $"\n\t\tFloors: {Wealth.WealthFloorsOnly}";
            msg += $"\n\tItems: {Wealth.WealthItems}";
            msg += $"\n\tPawns: {Wealth.WealthPawns}";

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label( rect, msg );
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.DrawHighlightIfMouseover( rect );
            if ( Widgets.ButtonInvisible( rect ) )
            {
                var valueCache = Traverse.Create( typeof( WealthWatcher ) ).Field( "cachedTerrainMarketValue" )
                    .GetValue<float[]>();
                float total = 0f;
                var terrains = Find.CurrentMap.terrainGrid.topGrid
                    .GroupBy( t => t )
                    .Select( t => new
                    {
                        def = t.Key,
                        count = t.Count(),
                        value = t.Key.GetStatValueAbstract( StatDefOf.MarketValue ),
                        vanillaValue = valueCache[t.Key.index]
                    } )
                    .Where( t => t.value > 0 || t.vanillaValue > 0 )
                    .OrderByDescending( t => t.count );

                foreach ( var terrain in terrains )
                {
                    Log.Message( $"{terrain.def.defName} x{terrain.count}, @{terrain.value} ({terrain.vanillaValue}): {terrain.count * terrain.value} ({terrain.count * terrain.vanillaValue})" );
                    total += terrain.count * terrain.value;
                }
                Log.Message(
                    $"Total: {total}, Vanilla total: {Traverse.Create( Wealth ).Method( "CalculateWealthFloors" ).GetValue()}" );
            }
        }
    }
#endif
}
