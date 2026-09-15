using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class score : MonoBehaviour
{
    public float team1;
    public float team2;

    public TextMeshPro mText1;
    public TextMeshPro mText2;

    // Start is called before the first frame update
    public void AddScore(int team)
    {
        print("Acessed");
        
        if (team == 1)
        {
            team1 += 1;
            mText1.text = team1.ToString();
        }
        if (team == 0)
        {
            team2 += 1;
            mText2.text = team2.ToString();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
