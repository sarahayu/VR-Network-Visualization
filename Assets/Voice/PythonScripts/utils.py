

def print_colored(text, color):
    """
    Print text in a specific color.
    
    Args:
        text (str): The text to print.
        color (str): The color name (e.g., 'red', 'green', 'blue').
    """
    colors = {
        'red': '\033[91m',
        'green': '\033[92m',
        'yellow': '\033[93m',
        'blue': '\033[94m',
        'magenta': '\033[95m',
        'cyan': '\033[96m',
        'white': '\033[97m',
        'reset': '\033[0m'
    }
    
    if color in colors:
        print(f"{colors[color]}{text}{colors['reset']}")
    else:
        print(text)  # Default to no color if the color is not recognized