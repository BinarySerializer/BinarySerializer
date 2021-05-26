using System;
using System.Collections.Generic;
using System.Linq;

namespace BinarySerializer 
{
    public class VersionTree<T>
        where T : Enum
    {
        public Node Root { get; set; }
        public Node Current { get; set; }

        public void Init() => Root.PropagateParents();

        public bool HasParent(T version) => Current.HasParent(version);
        public Node FindVersion(T version) => Root.FindVersion(version);

        public class Node
        {
            public Node(T version)
            {
                Version = version;
                ParentVersions = new HashSet<T>();
            }

            public T Version { get; set; }

            public Node[] Children { get; set; }

            protected HashSet<T> ParentVersions { get; set; }

            public bool HasParent(T version) => Version.Equals(version) || ParentVersions.Contains(version);

            public Node FindVersion(T version) 
            {
                if (Version.Equals(version)) 
                    return this;

                return Children?.Select(child => child.FindVersion(version)).FirstOrDefault(result => result != null);
            }

            public void PropagateParents() 
            {
                if (Children == null) 
                    return;

                foreach (var child in Children) 
                {
                    foreach (var pv in ParentVersions)
                        child.ParentVersions.Add(pv);

                    child.ParentVersions.Add(Version);
                    child.PropagateParents();
                }
            }

            public Node SetChildren(params Node[] nodes) 
            {
                Children = nodes;
                return this;
            }
        }
    }
}