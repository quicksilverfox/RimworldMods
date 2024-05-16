// ResearchProjectDef_Extensions.cs
// Copyright Karel Kroeze, 2019-2020

using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ResearchPowl
{
    public static class ResearchProjectDef_Extensions
    {
        public static List<ResearchProjectDef> Ancestors(this ResearchProjectDef research)
        {
            // keep a list of prerequites
            var prerequisites = new List<ResearchProjectDef>();
            if ( research.prerequisites.NullOrEmpty() ) return prerequisites;

            // keep a stack of prerequisites that should be checked
            var stack = new Stack<ResearchProjectDef>();
            var list = research.prerequisites;
            for (int i = list.Count; i-- > 0;)
            {
                var def = list[i];
                if (def != research) stack.Push(def);
            }

            // keep on checking everything on the stack until there is nothing left
            while (stack.Count > 0)
            {
                // add to list of prereqs
                var parent = stack.Pop();
                prerequisites.Add(parent);

                // add prerequitsite's prereqs to the stack
                if (!parent.prerequisites.NullOrEmpty())
                {
                    for (int i = list.Count; i-- > 0;)
                    {
                        var grandparent = list[i];
                        // but only if not a prerequisite of itself, and not a cyclic prerequisite
                        if (grandparent != parent && !prerequisites.Contains(grandparent)) stack.Push(grandparent);
                    }
                }
            }

            return new List<ResearchProjectDef>(prerequisites.Distinct());
        }
        
        public static ResearchNode ResearchNode( this ResearchProjectDef research )
        {
            var node = Tree.ResearchNodes().FirstOrDefault( n => n.Research == research );
            if ( node == null )
                Log.Error( "Node for {0} not found. Was it intentionally hidden or locked?", true, research.LabelCap );
            return node;
        }
    }
}