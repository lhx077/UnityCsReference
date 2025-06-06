// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Buffers;
using System.Collections.Generic;
using UnityEngine.Pool;
using Unity.Profiling;
using UnityEngine.Assertions;
using UnityEngine.Bindings;

namespace UnityEngine.UIElements.StyleSheets
{
    // Result of a single match between a selector and visual element.
    internal struct MatchResultInfo
    {
        public readonly bool success;
        public readonly PseudoStates triggerPseudoMask; // what pseudo states contributes to matching this selector
        public readonly PseudoStates dependencyPseudoMask; // what pseudo states if set, would have given a different result

        public MatchResultInfo(bool success, PseudoStates triggerPseudoMask, PseudoStates dependencyPseudoMask)
        {
            this.success = success;
            this.triggerPseudoMask = triggerPseudoMask;
            this.dependencyPseudoMask = dependencyPseudoMask;
        }
    }

    // Each struct represents on match for a visual element against a complex
    [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
    internal struct SelectorMatchRecord : IEquatable<SelectorMatchRecord>
    {
        public StyleSheet sheet;
        public int styleSheetIndexInStack;
        public StyleComplexSelector complexSelector;

        public SelectorMatchRecord(StyleSheet sheet, int styleSheetIndexInStack) : this()
        {
            this.sheet = sheet;
            this.styleSheetIndexInStack = styleSheetIndexInStack;
        }

        public static int Compare(SelectorMatchRecord a, SelectorMatchRecord b)
        {
            if (a.sheet.isDefaultStyleSheet != b.sheet.isDefaultStyleSheet)
                return a.sheet.isDefaultStyleSheet ? -1 : 1;

            int res = a.complexSelector.specificity.CompareTo(b.complexSelector.specificity);

            if (res == 0)
            {
                res = a.styleSheetIndexInStack.CompareTo(b.styleSheetIndexInStack);
            }

            if (res == 0)
            {
                res = a.complexSelector.orderInStyleSheet.CompareTo(b.complexSelector.orderInStyleSheet);
            }

            return res;
        }

        public bool Equals(SelectorMatchRecord other)
        {
            return Equals(sheet, other.sheet) && styleSheetIndexInStack == other.styleSheetIndexInStack && Equals(complexSelector, other.complexSelector);
        }

        public override bool Equals(object obj)
        {
            return obj is SelectorMatchRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(sheet, styleSheetIndexInStack, complexSelector);
        }
    }

    // Pure functions for the central logic of selector application
    static class StyleSelectorHelper
    {
        // This internal flag can be enabled to validate that the Bloom filter never rejects cases where
        // the exhaustive search returns a valid match. This is disabled by default, and is enabled from
        // styling unit tests.
        internal static bool s_VerifyBloomIntegrity = false;

        public static MatchResultInfo MatchesSelector(VisualElement element, StyleSelector selector)
        {
            bool match = true;

            StyleSelectorPart[] parts = selector.parts;
            int count = parts.Length;

            for (int i = 0; i < count && match; i++)
            {
                switch (parts[i].type)
                {
                    case StyleSelectorType.Wildcard:
                        break;
                    case StyleSelectorType.Class:
                        match = element.ClassListContains(parts[i].value);
                        break;
                    case StyleSelectorType.ID:
                        match = string.Equals(element.name, parts[i].value, StringComparison.Ordinal);
                        break;
                    case StyleSelectorType.Type:
                        //TODO: This tests fails to capture instances of sub-classes
                        match = string.Equals(element.typeName, parts[i].value, StringComparison.Ordinal);
                        break;
                    case StyleSelectorType.Predicate:
                        match = parts[i].tempData is UQuery.IVisualPredicateWrapper w && w.Predicate(element);
                        break;
                    case StyleSelectorType.PseudoClass:
                        // Selectors with invalid pseudo states should be rejected
                        if (selector.pseudoStateMask == StyleSelector.InvalidPseudoStateMask
                            || selector.negatedPseudoStateMask == StyleSelector.InvalidPseudoStateMask)
                        {
                            match = false;
                        }
                        break;
                    default: // ignore, all errors should have been warned before hand
                        match = false;
                        break;
                }
            }

            int triggerPseudoStateMask = 0;
            int dependencyPseudoMask = 0;

            bool saveMatch = match;

            if (saveMatch  && selector.pseudoStateMask != 0)
            {
                match = (selector.pseudoStateMask & (int)element.pseudoStates) == selector.pseudoStateMask;

                if (match)
                {
                    // the element matches this selector because it has those flags
                    dependencyPseudoMask = selector.pseudoStateMask;
                }
                else
                {
                    // if the element had those flags defined, it would match this selector
                    triggerPseudoStateMask = selector.pseudoStateMask;
                }
            }

            if (saveMatch && selector.negatedPseudoStateMask != 0)
            {
                match &= (selector.negatedPseudoStateMask & ~(int)element.pseudoStates) == selector.negatedPseudoStateMask;

                if (match)
                {
                    // the element matches this selector because it does not have those flags
                    triggerPseudoStateMask |= selector.negatedPseudoStateMask;
                }
                else
                {
                    // if the element didn't have those flags, it would match this selector
                    dependencyPseudoMask |= selector.negatedPseudoStateMask;
                }
            }

            return new MatchResultInfo(match, (PseudoStates)triggerPseudoStateMask, (PseudoStates)dependencyPseudoMask);
        }

        public static bool MatchRightToLeft(VisualElement element, StyleComplexSelector complexSelector, Action<VisualElement, MatchResultInfo> processResult)
        {
            // see https://speakerdeck.com/constellation/css-jit-just-in-time-compiled-css-selectors-in-webkit for
            // a detailed explaination of the algorithm

            var current = element;
            int nextIndex = complexSelector.selectors.Length - 1;
            VisualElement saved = null;
            int savedIdx = -1;

            // go backward
            while (nextIndex >= 0)
            {
                if (current == null)
                    break;

                MatchResultInfo matchInfo = MatchesSelector(current, complexSelector.selectors[nextIndex]);
                processResult(current, matchInfo);

                if (!matchInfo.success)
                {
                    // if we have a descendant relationship, keep trying on the parent
                    // i.e., "div span", div failed on this element, try on the parent
                    // happens earlier than the backtracking saving below
                    if (nextIndex < complexSelector.selectors.Length - 1 &&
                        complexSelector.selectors[nextIndex + 1].previousRelationship == StyleSelectorRelationship.Descendent)
                    {
                        current = current.parent;
                        continue;
                    }

                    // otherwise, if there's a previous relationship, it's a 'child' one. backtrack from the saved point and try again
                    // ie.  for "#x > .a .b", #x failed, backtrack to .a on the saved element
                    if (saved != null)
                    {
                        current = saved;
                        nextIndex = savedIdx;
                        continue;
                    }

                    break;
                }

                // backtracking save
                // for "a > b c": we're considering the b matcher. c's previous relationship is Descendent
                // save the current element parent to try to match b again
                if (nextIndex < complexSelector.selectors.Length - 1
                    && complexSelector.selectors[nextIndex + 1].previousRelationship == StyleSelectorRelationship.Descendent)
                {
                    saved = current.parent;
                    savedIdx = nextIndex;
                }

                // from now, the element is a match
                if (--nextIndex < 0)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        static void TestSelectorLinkedList(StyleComplexSelector currentComplexSelector,
            List<SelectorMatchRecord> matchedSelectors, StyleMatchingContext context, ref SelectorMatchRecord record)
        {
            {
                while (currentComplexSelector != null)
                {
                    bool isCandidate = true;
                    bool isMatchRightToLeft = false;

                    if (!currentComplexSelector.isSimple)
                    {
                        isCandidate = context.ancestorFilter.IsCandidate(currentComplexSelector);
                    }

                    if (isCandidate || s_VerifyBloomIntegrity)
                    {
                        isMatchRightToLeft = MatchRightToLeft(context.currentElement, currentComplexSelector, context.processResult);
                    }

                    // This verifies that the Bloom filter never rejects a valid complex selector.
                    if (s_VerifyBloomIntegrity)
                    {
                        Assert.IsTrue(isCandidate || !isMatchRightToLeft, "The Bloom filter returned a false negative match.");
                    }

                    if (isMatchRightToLeft)
                    {
                        record.complexSelector = currentComplexSelector;
                        matchedSelectors.Add(record);
                    }

                    currentComplexSelector = currentComplexSelector.nextInTable;
                }
            }
        }

        static void FastLookup(IDictionary<string, StyleComplexSelector> table, List<SelectorMatchRecord> matchedSelectors, StyleMatchingContext context, string input, ref SelectorMatchRecord record)
        {
            if (table.TryGetValue(input, out StyleComplexSelector currentComplexSelector))
            {
                TestSelectorLinkedList(currentComplexSelector, matchedSelectors, context, ref record);
            }
        }

        public static void FindMatches(StyleMatchingContext context, List<SelectorMatchRecord> matchedSelectors)
        {
            // To support having the root pseudo states set for style sheets added onto an element
            // we need to find which sheets belongs to the element itself.
            VisualElement element = context.currentElement;
            int parentSheetIndex =  context.styleSheetCount - 1;
            if (element.styleSheetList != null)
            {
                // The number of style sheet for an element is the count of the styleSheetList + all imported style sheet
                int elementSheetCount = element.styleSheetList.Count;
                for (var i = 0; i < element.styleSheetList.Count; i++)
                {
                    var elementSheet = element.styleSheetList[i];
                    if (elementSheet.flattenedRecursiveImports != null)
                        elementSheetCount += elementSheet.flattenedRecursiveImports.Count;
                }

                parentSheetIndex -= elementSheetCount;
            }

            FindMatches(context, matchedSelectors, parentSheetIndex);
        }

        struct SelectorWorkItem
        {
            public StyleSheet.OrderedSelectorType type;
            public string input;

            public SelectorWorkItem(StyleSheet.OrderedSelectorType type, string input)
            {
                this.type = type;
                this.input = input;
            }
        }

        public static void FindMatches(StyleMatchingContext context, List<SelectorMatchRecord> matchedSelectors, int parentSheetIndex)
        {
            Debug.Assert(matchedSelectors.Count == 0);

            Debug.Assert(context.currentElement != null, "context.currentElement != null");

            var toggleRoot = false;
            var processedStyleSheets = HashSetPool<StyleSheet>.Get();
            var workItems = ListPool<SelectorWorkItem>.Get();

            try
            {
                var element = context.currentElement;
                workItems.Add(new (StyleSheet.OrderedSelectorType.Type, element.typeName));

                if (!string.IsNullOrEmpty(element.name))
                    workItems.Add(new (StyleSheet.OrderedSelectorType.Name, element.name));
                List<string> classList = element.GetClassesForIteration();
                int classCount = classList.Count;
                for (int i = 0; i < classCount; i++)
                {
                    workItems.Add(new (StyleSheet.OrderedSelectorType.Class, classList[i]));
                }

                for (var i = context.styleSheetCount - 1; i >= 0; --i)
                {
                    var styleSheet = context.GetStyleSheetAt(i);
                    if (!processedStyleSheets.Add(styleSheet))
                        continue;

                    // If the sheet is added on the element consider it as :root
                    if (i > parentSheetIndex)
                    {
                        element.pseudoStates |= PseudoStates.Root;
                        toggleRoot = true;
                    }
                    else
                        element.pseudoStates &= ~PseudoStates.Root;

                    var record = new SelectorMatchRecord(styleSheet, i);

                    for (int j = 0; j < workItems.Count; j++)
                    {
                        var item = workItems[j];

                        if ((styleSheet.nonEmptyTablesMask & (1 << (int)item.type)) == 0)
                            continue;

                        var table = styleSheet.tables[(int)item.type];

                        FastLookup(table, matchedSelectors, context, item.input, ref record);
                    }

                    if (toggleRoot)
                    {
                        TestSelectorLinkedList(styleSheet.firstRootSelector, matchedSelectors, context, ref record);
                    }

                    TestSelectorLinkedList(styleSheet.firstWildCardSelector, matchedSelectors, context, ref record);
                }

                if (toggleRoot)
                    element.pseudoStates &= ~PseudoStates.Root;
            }
            finally
            {
                ListPool<SelectorWorkItem>.Release(workItems);
                HashSetPool<StyleSheet>.Release(processedStyleSheets);
            }
        }
    }
}
