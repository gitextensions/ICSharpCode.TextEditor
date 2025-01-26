// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Mike Krüger" email="mike@icsharpcode.net"/>
//     <version>$Revision$</version>
// </file>

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ICSharpCode.TextEditor.Document
{
    public class HighlightingManager
    {
        // hash table from extension name to highlighting definition,
        // OR from extension name to Pair SyntaxMode,ISyntaxModeFileProvider

        private readonly Dictionary<string, string> extensionsToName = new Dictionary<string, string>();
        private readonly List<ISyntaxModeFileProvider> syntaxModeFileProviders = new List<ISyntaxModeFileProvider>();

        static HighlightingManager()
        {
            Manager = new HighlightingManager();
            Manager.AddSyntaxModeFileProvider(new ResourceSyntaxModeProvider());
        }

        public HighlightingManager()
        {
            CreateDefaultHighlightingStrategy();
        }

        public Dictionary<string, object> HighlightingDefinitions { get; } = new Dictionary<string, object>();

        public static HighlightingManager Manager { get; }

        public DefaultHighlightingStrategy DefaultHighlighting => (DefaultHighlightingStrategy)HighlightingDefinitions["Default"];

        public void AddSyntaxModeFileProvider(ISyntaxModeFileProvider syntaxModeFileProvider)
        {
            foreach (var syntaxMode in syntaxModeFileProvider.SyntaxModes)
            {
                HighlightingDefinitions[syntaxMode.Name] = new DictionaryEntry(syntaxMode, syntaxModeFileProvider);
                foreach (var extension in syntaxMode.Extensions)
                    extensionsToName[extension.ToUpperInvariant()] = syntaxMode.Name;
            }

            if (!syntaxModeFileProviders.Contains(syntaxModeFileProvider))
                syntaxModeFileProviders.Add(syntaxModeFileProvider);
        }

        public void AddHighlightingStrategy(IHighlightingStrategy highlightingStrategy)
        {
            HighlightingDefinitions[highlightingStrategy.Name] = highlightingStrategy;
            foreach (var extension in highlightingStrategy.Extensions)
                extensionsToName[extension.ToUpperInvariant()] = highlightingStrategy.Name;
        }

        public void ReloadSyntaxModes()
        {
            HighlightingDefinitions.Clear();
            extensionsToName.Clear();
            CreateDefaultHighlightingStrategy();
            foreach (ISyntaxModeFileProvider provider in syntaxModeFileProviders)
            {
                provider.UpdateSyntaxModeList();
                AddSyntaxModeFileProvider(provider);
            }

            OnReloadSyntaxHighlighting(EventArgs.Empty);
        }

        private void CreateDefaultHighlightingStrategy()
        {
            var defaultHighlightingStrategy = DefaultHighlightingStrategy.Default;
            defaultHighlightingStrategy.Extensions = new string[] { };
            defaultHighlightingStrategy.Rules.Add(new HighlightRuleSet());
            HighlightingDefinitions["Default"] = defaultHighlightingStrategy;
        }

        private IHighlightingStrategy LoadDefinition(DictionaryEntry entry)
        {
            var syntaxMode = (SyntaxMode)entry.Key;
            var syntaxModeFileProvider = (ISyntaxModeFileProvider)entry.Value;

            DefaultHighlightingStrategy highlightingStrategy = null;
            try
            {
                var reader = syntaxModeFileProvider.GetSyntaxModeFile(syntaxMode);
                if (reader == null)
                    throw new HighlightingDefinitionInvalidException("Could not get syntax mode file for " + syntaxMode.Name);
                highlightingStrategy = HighlightingDefinitionParser.Parse(syntaxMode, reader);
                if (highlightingStrategy.Name != syntaxMode.Name)
                    throw new HighlightingDefinitionInvalidException("The name specified in the .xshd '" + highlightingStrategy.Name + "' must be equal the syntax mode name '" + syntaxMode.Name + "'");
            }
            finally
            {
                if (highlightingStrategy == null)
                    highlightingStrategy = DefaultHighlighting;
                HighlightingDefinitions[syntaxMode.Name] = highlightingStrategy;
                highlightingStrategy.ResolveReferences();
            }

            return highlightingStrategy;
        }

        internal KeyValuePair<SyntaxMode, ISyntaxModeFileProvider> FindHighlighterEntry(string name)
        {
            foreach (ISyntaxModeFileProvider provider in syntaxModeFileProviders)
            foreach (var mode in provider.SyntaxModes)
                if (mode.Name == name)
                    return new KeyValuePair<SyntaxMode, ISyntaxModeFileProvider>(mode, provider);
            return default;
        }

        public IHighlightingStrategy FindHighlighter(string name)
        {
            if (HighlightingDefinitions.TryGetValue(name, out var def))
            {
                switch (def)
                {
                    case DictionaryEntry entry:
                        return LoadDefinition(entry);
                    case IHighlightingStrategy strategy:
                        return strategy;
                }
            }

            return DefaultHighlighting;
        }

        public IHighlightingStrategy FindHighlighterForFile(string fileName)
        {
            var highlighterName = extensionsToName.FirstOrDefault(e => fileName.EndsWith(e.Key, StringComparison.OrdinalIgnoreCase));
            return highlighterName.Key == null ? DefaultHighlighting : FindHighlighter(highlighterName.Value);
        }

        protected virtual void OnReloadSyntaxHighlighting(EventArgs e)
        {
            ReloadSyntaxHighlighting?.Invoke(this, e);
        }

        public event EventHandler ReloadSyntaxHighlighting;
    }
}