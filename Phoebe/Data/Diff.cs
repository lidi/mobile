﻿using System;
using System.Linq;
using System.Collections.Generic;

namespace Toggl.Phoebe.Data
{
    public enum DiffComparison {
        Different,
        Same,
        Updated
    }

    public enum DiffType {
        Copy,
        Add,
        Remove,
        Replace,
        Move
    }

    public enum DiffMove {
        None,
        Backward,
        Forward
    }

    public interface IDiffComparable
    {
        DiffComparison Compare (IDiffComparable other);
    }

    public class DiffSection<T>
    {
        public DiffType Type { get; set; }
        public int OldIndex { get; set; }
        public int NewIndex { get; set; }
        public T OldItem { get; set; }
        public T NewItem { get; set; }
        public DiffMove Move { get; set; }

        /// <summary>Used only for internal purposes in Diff algorithm</summary>
        public DiffSection<T> Link { get; set; }

        public bool IsMove
        {
            get { return Move != DiffMove.None; }
        }

        public DiffSection (DiffType type, int oldIndex, T oldItem, int newIndex, T newItem)
        {
            Type = type;
            OldIndex = oldIndex;
            OldItem = oldItem;
            NewIndex = newIndex;
            NewItem = newItem;
        }

        public override string ToString ()
        {
            return string.Format ("[{0}, NewIndex={1}, NewItem={2}, OldIndex={3}, OldItem={4}]",
                                  Enum.GetName (typeof (DiffType), Type), NewIndex, NewItem, OldIndex, OldItem);
        }
    }

    public struct LongestCommonSubstringResult {
        public readonly bool Success;
        public readonly int PositionA;
        public readonly int PositionB;
        public readonly int Length;

        public LongestCommonSubstringResult (int posA, int posB, int length)
        {
            Success = true;
            PositionA = posA;
            PositionB = posB;
            Length = length;
        }

        public override string ToString()
        {
            return Success
                   ? string.Format ("LCS ({0}, {1} x{2})", PositionA, PositionB, Length)
                   : "LCS (-)";
        }
    }

    // Adapted from http://devdirective.com/post/115/creating-a-reusable-though-simple-diff-implementation-in-csharp-part-3
    public static class Diff
    {
        /// <summary>
        /// Calculates move and replace operation (besides add & remove) and makes indices linear
        /// </summary>
        public static IList<DiffSection<T>> CalculateExtra<T> (
            IList<T> listA, IList<T> listB, int startA = 0, int endA = -1, int startB = 0, int endB = -1)
        where T : IDiffComparable
        {
            var diffs = Calculate (listA, listB, startA, endA, startB, endB);

            var diffsWithReplace = diffs.Select (x => {
                if (x.Type == DiffType.Copy && x.OldItem.Compare (x.NewItem) != DiffComparison.Same) {
                    x.Type = DiffType.Replace;
                }
                return x;
            });

            var diffsDic = diffsWithReplace
                           .GroupBy (diff => diff.Type)
                           .ToDictionary (gr => gr.Key, gr => gr.ToList ());

            // Calculate Move diffs
            if (diffsDic.ContainsKey (DiffType.Add) && diffsDic.ContainsKey (DiffType.Remove)) {
                foreach (var addDiff in diffsDic[DiffType.Add]) {
                    var rmDiff = diffsDic [DiffType.Remove].FirstOrDefault (x =>
                                 listA [x.OldIndex].Compare (addDiff.NewItem) != DiffComparison.Different);

                    if (rmDiff != null) {
                        addDiff.Link = rmDiff;
                        rmDiff.Link = addDiff;
                        addDiff.OldIndex = rmDiff.NewIndex;
                        rmDiff.Move = addDiff.Move = rmDiff.NewIndex < addDiff.NewIndex
                                                     ? DiffMove.Forward : DiffMove.Backward;
                    }
                }
            }

            var fwOffset = 0;
            var bwOffsetDic = new Dictionary<DiffSection<T>, int> ();
            return diffsDic
                   .SelectMany (x => x.Value)
                   .Where (x => x.Type != DiffType.Copy)
                   .OrderBy (x => x.NewIndex)
                   .ThenBy (x => x.Type == DiffType.Remove ? -1 : 0)
                   // Add offset to indices so events can be raised linearly without conflicting with moves
            .Select (x => {
                if (x.Type == DiffType.Add) {
                    if (x.IsMove) {
                        if (x.Move == DiffMove.Forward) {
                            fwOffset--;
                        } else {
                            bwOffsetDic.Add (x.Link, 0);
                        }
                        x.Link = null;  // Reference is not needed any more
                        x.Type = DiffType.Move;
                        x.OldIndex += fwOffset;
                    }
                    foreach (var k in bwOffsetDic.Keys.ToList ()) {
                        bwOffsetDic[k] -= 1;
                    }
                } else if (x.Type == DiffType.Remove) {
                    if (!x.IsMove) {
                        foreach (var k in bwOffsetDic.Keys.ToList ()) {
                            bwOffsetDic[k] += 1;
                        }
                    } else if (x.Move == DiffMove.Forward) {
                        fwOffset++;
                    } else { // Move == Diff.Backward
                        x.Link.OldIndex += bwOffsetDic[x];
                        bwOffsetDic.Remove (x);
                    }
                }
                x.NewIndex += fwOffset;
                return x;
            })
            .Where (x => ! (x.Type == DiffType.Remove && x.IsMove))
            .ToList ();
        }

        public static IEnumerable<DiffSection<T>> Calculate<T> (
            IList<T> listA, IList<T> listB, int startA = 0, int endA = -1, int startB = 0, int endB = -1)
        where T : IDiffComparable
        {
            endA = endA > -1 ? endA : listA.Count;
            endB = endB > -1 ? endB : listB.Count;

            var lcs = FindLongestCommonSubstring (
                          listA, listB, startA, endA, startB, endB);

            if (lcs.Success) {
                // deal with the section before
                var sectionsBefore =
                    Calculate (listA, listB, startA, lcs.PositionA, startB, lcs.PositionB);

                foreach (var section in sectionsBefore) {
                    yield return section;
                }

                // output the copy operation
                for (int i = 0; i < lcs.Length; i++) {
                    int indexA = lcs.PositionA + i, indexB = lcs.PositionB + i;
                    yield return new DiffSection<T> (DiffType.Copy, indexA, listA[indexA], indexB, listB[indexB]);
                }

                // deal with the section after
                var sectionsAfter =
                    Calculate (listA, listB, lcs.PositionA + lcs.Length, endA, lcs.PositionB + lcs.Length, endB);

                foreach (var section in sectionsAfter) {
                    yield return section;
                }

                yield break;
            }

            // if we get here, no LCS
            if (endA > startA) {
                // we got content from first collection --> deleted
                for (int i = 0; i < endA - startA; i++) {
                    yield return new DiffSection<T> (DiffType.Remove, startA + i, listA[startA + i], startB, default (T));
                }
            }

            if (endB > startB) {
                // we got content from second collection --> inserted
                for (int i = 0; i < endB - startB; i++) {
                    yield return new DiffSection<T> (DiffType.Add, startA, default (T), startB + i, listB[startB + i]);
                }
            }
        }

        private static LongestCommonSubstringResult FindLongestCommonSubstring<T> (
            IList<T> listA, IList<T> listB, int startA, int endA, int startB, int endB)
        where T : IDiffComparable
        {
            // default result, if we can't find anything
            var result = new LongestCommonSubstringResult();

            for (int index1 = startA; index1 < endA; index1++) {
                for (int index2 = startB; index2 < endB; index2++) {
                    if (listA[index1].Compare (listB[index2]) != DiffComparison.Different) {
                        int length = CountEqual (
                                         listA, listB, index1, endA, index2, endB);

                        // Is longer than what we already have --> record new LCS
                        if (length > result.Length) {
                            result = new LongestCommonSubstringResult (index1, index2, length);
                        }
                    }
                }
            }

            return result;
        }

        private static int CountEqual<T> (IList<T> listA, IList<T> listB, int startA, int endA, int startB, int endB)
        where T : IDiffComparable
        {
            int length = 0;
            while (startA < endA && startB < endB) {
                if (listA[startA].Compare (listB[startB]) == DiffComparison.Different) {
                    break;
                }
                startA++;
                startB++;
                length++;
            }
            return length;
        }
    }
}
