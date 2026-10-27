using Vector2 = UnityEngine.Vector2;
using UnityEngine;

public class Node 
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public bool walkable; //if you can walk through the node
    public int xgrid; //x corodinate
    public int ygrid; // y coordinate
    public Vector2 worldPosition;

    public int gCost; //the cost to go the node from the start
    public int hCost; // h = heuristic, estimated cost to target node
    public int fCost => gCost +hCost; // the total cost
    public Node parent; //the previous node used to find the final path

    public Node(bool _walkable, Vector2 _worldPos, int _xgrid, int _ygrid)
    {
        walkable = _walkable;
        worldPosition = _worldPos;
        xgrid = _xgrid;
        ygrid = _ygrid;
    }


}
