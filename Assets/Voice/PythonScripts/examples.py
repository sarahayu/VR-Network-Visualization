EXAMPLES = [
    {
        "user": "Highlight the node with the highest degree",
        "assistant": {
            "query": "list.OrderByDescending(x => x.Degree).Take(1).ToList(); // Highlight currently selected node"
        }
    },
    {
        "user": "Select the 5 least well-connected nodes",
        "assistant": {
            "query": "list.OrderBy(x => x.Degree).Take(5).ToList(); // Select the currently active nodes"
        }
    },
    {
        "user": "Show me the 3 smallest groups",
        "assistant": {
            "query": "list.GroupBy(x => x.GroupId).OrderBy(g => g.Count()).Take(3).SelectMany(g => g).ToList(); // Highlight nodes and their group"
        }
    },
    {
        "user": "Create a new group containing the top 10 most bullied students",
        "assistant": {
            "query": "list.OrderByDescending(x => x.BullyScore).Take(10).ToList(); // Detach → Regroup → Apply force-directed layout"
        }
    },
    {
        "user": "Change to a 3D layout",
        "assistant": {
            "query": "list.ToList(); // Apply 'Force-Directed 3D' layout to all nodes"
        }
    },
    {
        "user": "Make a new surface for me",
        "assistant": {
            "query": "// Create new surface object in the scene with available space"
        }
    },
    {
        "user": "Place all the nodes in the smallest group onto my work surface",
        "assistant": {
            "query": "list.GroupBy(x => x.GroupId).OrderBy(g => g.Count()).First().ToList(); // Project these nodes onto a work surface"
        }
    }
]
