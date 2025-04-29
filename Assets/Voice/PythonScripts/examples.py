EXAMPLES = [
    {
        "user": "Change to the spherical layout",
        "assistant": {
            "steps": [
                {
                    "Task": "Layout",
                    "Attribute": "Spherical",
                    "Number": "",
                    "Sequence": "0"
                }
            ]
        }
    },
    {
        "user": "Highlight the node with the highest degree",
        "assistant": {
            "steps": [
                {
                    "Task": "Select",
                    "Attribute": "Degree",
                    "Number": "1",
                    "Sequence": "0"
                }
            ]
        }
    },
    {
        "user": "Select the 5 least well-connected nodes",
        "assistant": {
            "steps": [
                {
                    "Task": "Select",
                    "Attribute": "Degree",
                    "Number": "-5",
                    "Sequence": "0"
                }
            ]
        }
    },
    {
        "user": "Show me the 3 smallest groups",
        "assistant": {
            "steps": [
                {
                    "Task": "Select",
                    "Attribute": "Smallest Groups",
                    "Number": "-3",
                    "Sequence": "0"
                }
            ]
        }
    },
    {
        "user": "Create a new group containing the top 10 most bullied students",
        "assistant": {
            "steps": [
                {
                    "Task": "Create Group",
                    "Attribute": "Most Bullied",
                    "Number": "10",
                    "Sequence": "0"
                }
            ]
        }
    },
    {
        "user": "Make a new surface for me",
        "assistant": {
            "steps": [
                {
                    "Task": "Surface",
                    "Attribute": "New",
                    "Number": "",
                    "Sequence": "0"
                }
            ]
        }
    },
    {
        "user": "Move all the nodes in the smallest group onto the work surface",
        "assistant": {
            "steps": [
                {
                    "Task": "Select",
                    "Attribute": "Smallest Group",
                    "Number": "1",
                    "Sequence": "0"
                },
                {
                    "Task": "Move",
                    "Attribute": "",
                    "Number": "",
                    "Sequence": "1"
                }
            ]
        }
    }
]
