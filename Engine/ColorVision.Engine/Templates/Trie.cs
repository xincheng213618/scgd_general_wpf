using System.Collections.Generic;

namespace ColorVision.Engine.Templates
{
    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = new Dictionary<char, TrieNode>();
        public bool IsEndOfWord { get; set; }
    }
    public class Trie
    {
        private readonly TrieNode _root;

        public Trie()
        {
            _root = new TrieNode();
        }

        public void Insert(string word)
        {
            var current = _root;
            foreach (var ch in word)
            {
                if (!current.Children.TryGetValue(ch, out TrieNode? value))
                {
                    value = new TrieNode();
                    current.Children[ch] = value;
                }
                current = value;
            }
            current.IsEndOfWord = true;
        }

        public bool Search(string word)
        {
            var current = _root;
            foreach (var ch in word)
            {
                if (!current.Children.TryGetValue(ch, out TrieNode? value))
                {
                    return false;
                }
                current = value;
            }
            return current.IsEndOfWord;
        }

        public bool StartsWith(string prefix)
        {
            var current = _root;
            foreach (var ch in prefix)
            {
                if (!current.Children.TryGetValue(ch, out TrieNode? value))
                {
                    return false;
                }
                current = value;
            }
            return true;
        }

        // 获取所有以给定前缀开始的模板名
        public List<string> GetRecommendations(string prefix)
        {
            var current = _root;
            foreach (var ch in prefix)
            {
                if (!current.Children.TryGetValue(ch, out TrieNode? value))
                {
                    return new List<string>();
                }
                current = value;
            }

            var recommendations = new List<string>();
            GetWordsFromNode(current, prefix, recommendations);
            return recommendations;
        }

        private void GetWordsFromNode(TrieNode node, string prefix, List<string> results)
        {
            if (node.IsEndOfWord)
            {
                results.Add(prefix);
            }

            foreach (var kvp in node.Children)
            {
                GetWordsFromNode(kvp.Value, prefix + kvp.Key, results);
            }
        }
    }
}
