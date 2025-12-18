import networkx as nx
import UnityEngine;

G = nx.Graph()
G.add_nodes_from([(4, {"color": "red"}), (5, {"color": "green"})])
UnityEngine.Debug.Log(G.number_of_nodes())