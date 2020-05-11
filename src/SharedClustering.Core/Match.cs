using System.Collections.Generic;

namespace SharedClustering.Core
{
    /// <summary>
    /// An object that describes the core properties of a DNA match.
    /// 
    /// Only the TestGuid and SharedCentimorgans properties are required. All of the others can be omitted.
    /// </summary>
    public class Match
    {
        /// <summary>
        /// The user-visible name displayed for the person who administers this match's test. 
        /// May be null or blank. If null or blank, this match's test taker is assumed to administer their own test.
        /// </summary>
        public string MatchTestAdminDisplayName { get; set; }

        /// <summary>
        /// The user-visible name displayed for this match.
        /// May be null or blank.
        /// </summary>
        public string MatchTestDisplayName { get; set; }

        /// <summary>
        /// A uninque identifier for this test.
        /// REQUIRED. This string does not need to be a GUID, but it does need to be unique across all tests being clustered.
        /// </summary>
        public string TestGuid { get; set; }

        /// <summary>
        /// The number of centimorgans shared between this match and the main test taker.
        /// REQUIRED.
        /// </summary>
        public double SharedCentimorgans { get; set; }

        /// <summary>
        /// The number of DNA segments shared between this match and the main test taker.
        /// </summary>
        public int SharedSegments { get; set; }

        /// <summary>
        /// The longest DNA segment shared between this match and the main test taker.
        /// </summary>
        public double LongestBlock { get; set; }

        /// <summary>
        /// The type of family tree provided by this match.
        /// </summary>
        public TreeType TreeType { get; set; }

        /// <summary>
        /// The location of the family tree provided by this match.
        /// </summary>
        public string TreeUrl { get; set; }

        /// <summary>
        /// The size of the family tree provided by this match.
        /// </summary>
        public int TreeSize { get; set; }

        /// <summary>
        /// Whether this match has any ancestors known to be shared with the test taker.
        /// </summary>
        public bool HasCommonAncestors { get; set; }

        /// <summary>
        /// A list of names of ancestors know to be shared between this match and the test taker
        /// </summary>
        public List<string> CommonAncestors { get; set; }

        /// <summary>
        /// Whether this match has been starred.
        /// </summary>
        public bool Starred { get; set; }

        /// <summary>
        /// Whether this match has a hint.
        /// </summary>
        public bool HasHint { get; set; }

        /// <summary>
        /// Arbitrary text entered by the test taker to describe this match.
        /// </summary>
        public string Note { get; set; }


        /// <summary>
        /// IDs of any tags assigned to this match.
        /// </summary>
        public List<int> TagIds { get; set; }


        /// <summary>
        /// Whether this match is the father of the test taker.
        /// </summary>
        public bool IsFather { get; set; }

        /// <summary>
        /// Whether this match is the mother of the test taker.
        /// </summary>
        public bool IsMother { get; set; }


        public string Name 
            => string.IsNullOrWhiteSpace(MatchTestAdminDisplayName)
            ? MatchTestDisplayName
            : MatchTestDisplayName == MatchTestAdminDisplayName
                ? MatchTestAdminDisplayName 
                : $"{MatchTestDisplayName} (managed by {MatchTestAdminDisplayName})";
    }
}
