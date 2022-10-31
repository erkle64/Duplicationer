namespace Duplicationer
{
    internal static class TabletHelper
    {
        public static void SetTabletTextAnalyzer(string text)
        {
            GameRoot.getTabletHH().uiText_analyzer.setText(text);
        }

        public static void SetTabletTextLastCopiedConfig(string text)
        {
            GameRoot.getTabletHH().uiText_lastCopiedConfig.setText(text);
        }

        public static void SetTabletTextQuickActions(string text)
        {
            GameRoot.getTabletHH().uiText_quickActions.setText(text);
        }

        public static void SetTabletTexts(string analyzer, string quickActions, string lastCopiedConfig)
        {
            HandheldTabletHH tabletHH = GameRoot.getTabletHH();
            tabletHH.uiText_analyzer.setText(analyzer);
            tabletHH.uiText_quickActions.setText(quickActions);
            tabletHH.uiText_lastCopiedConfig.setText(lastCopiedConfig);
        }
    }
}