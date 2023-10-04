using System.Collections.Generic;
using UnityEngine.Rendering;

namespace WaterSystem
{
    public class DebugTooling
    {
        // GUI
        internal class Styles
        {
            internal const string PanelTitle = "BA Water System";

            internal static readonly DebugUI.Widget.NameAndTooltip ShaderDebug = new DebugUI.Widget.NameAndTooltip
            {
                name = "Shader Debug",
                tooltip = "Debug",
            };
        }

        // Data
        private static int shadingDebug = 0;

        public static void Create()
        {
            // Create a list of widgets
            var widgetList = new List<DebugUI.Widget>();

            // Add a checkbox widget to the list of widgets
            widgetList.AddRange(new DebugUI.Widget[]
            {
                new DebugUI.EnumField
                {
                    nameAndTooltip = Styles.ShaderDebug,
                    autoEnum = typeof(Data.DebugShading),

                    getter = () => shadingDebug,
                    setter = value => shadingDebug = value,

                    getIndex = () => shadingDebug,
                    setIndex = value => shadingDebug = value,

                    onValueChanged = ShaderDebugChanged,
                },
            });
            // Create a new panel (tab) in the Rendering Debugger
            var panel = DebugManager.instance.GetPanel(Styles.PanelTitle, createIfNull: true);

            // Add the widgets to the panel
            panel.children.Add(widgetList.ToArray());
        }

        private static void ShaderDebugChanged(DebugUI.Field<int> field, int value)
        {
            //Ocean.SetDebugMode((Data.DebugShading) value); //TODO fix removal of the ocean
        }

        public static void Dispose()
        {
            DebugManager.instance.RemovePanel(Styles.PanelTitle);
        }
    }
}