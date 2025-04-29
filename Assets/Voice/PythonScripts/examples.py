EXAMPLES = [
    {
        "user": "Highlight the node with the highest degree",
        "assistant": {
            "query": "list.AsQueryable().OrderBy(\"Degree descending\").FirstOrDefault()"
        }
    },
    {
        "user": "Select the 5 least well-connected nodes",
        "assistant": {
            "query": "list.AsQueryable().OrderBy(\"Degree\").Take(5).ToList()"
        }
    },
    {
        "user": "Show me the 3 smallest groups",
        "assistant": {
            "query": "list.AsQueryable().GroupBy(\"GroupId\").OrderBy(\"Count() ascending\").Take(3).SelectMany(\"it\").ToList()"
        }
    },
    {
        "user": "Create a new group containing the top 10 most bullied students",
        "assistant": {
            "query": "list.AsQueryable().OrderBy(\"BullyScore descending\").Take(10).ToList()"
        }
    },
    {
        "user": "Change to a 3D layout",
        "assistant": {
            "query": "list.AsQueryable().ToList()"
        }
    },
    {
        "user": "Make a new surface for me",
        "assistant": {
            "query": "" 
        }
    },
    {
        "user": "Place all the nodes in the smallest group onto my work surface",
        "assistant": {
            "query": "list.AsQueryable().GroupBy(\"GroupId\").OrderBy(\"Count() ascending\").First().AsQueryable().ToList()"
        }
    }
]
