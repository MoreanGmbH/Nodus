using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEngine;

namespace Nodus.Localization.Editor {
    public static class MainTableCsvImporter {
        const string Path = "Assets/UI/Localization/Tables/MainTable.csv";

        [MenuItem("Nodus/Localization/Import MainTable CSV")]
        static void Import() {
            var table = LocalizationEditorSettings.GetStringTableCollection("MainTable");
            if (table == null)
                throw new InvalidOperationException("Unity Localization collection 'MainTable' was not found.");

            using (var reader = new StreamReader(Path, Encoding.UTF8))
                Csv.ImportInto(reader, table, createUndo: true, removeMissingEntries: false);

            AssetDatabase.SaveAssets();
            Debug.Log("MainTable: CSV merged into English and German string tables.");
        }
    }
}
