using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#region Copyright
// MIT License
// 
// Copyright (c) 2023 David María Arribas
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using System;
using NavigationDJIA.Interfaces;
using NavigationDJIA.World;
using QMind.Interfaces;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using System.IO;
using System.Linq;

namespace Grupo9
{
    public class MyTrainer : IQMindTrainer
    {
        public int CurrentEpisode { get; }
        public int CurrentStep { get; private set; }
        public CellInfo AgentPosition { get; private set; }
        public CellInfo OtherPosition { get; private set; }
        public float Return { get; }
        public float ReturnAveraged { get; }
        public event EventHandler OnEpisodeStarted;
        public event EventHandler OnEpisodeFinished;

        private INavigationAlgorithm _navigationAlgorithm;
        private int counter = 0;

        //Tabla Q
        public float[,] tableQ { get; set; }
        private int nRows { get; set; }
        private int nCols { get; set; }

        QMind.QMindTrainerParams parameters;

        public bool explorar = true;

        private string filePath = "Assets/Scripts/Grupo9/tableQ.csv";

        public void Initialize(QMind.QMindTrainerParams qMindTrainerParams, WorldInfo worldInfo, INavigationAlgorithm navigationAlgorithm)
        {
            this.nRows = worldInfo.WorldSize.x * 1000 + worldInfo.WorldSize.y * 100 + worldInfo.WorldSize.x * 10 + worldInfo.WorldSize.y; //Numero de acciones posibles (izquierda, derecha, arriba, abajo)
            this.nCols = 4; //Cálculo del grid 
            this.tableQ = new float[nRows, nCols];

           
                for (int i = 0; i < this.nRows; i++)
                {
                    for (int j = 0; j < this.nCols; j++)
                    {
                        this.tableQ[i, j] = 0.0f;

                    }
                }
            
            this.parameters = qMindTrainerParams;
            Debug.Log("MyTrainer: initialized");

            Debug.Log("nRows: " + nRows);
            Debug.Log("nCols: " + nCols);
            _navigationAlgorithm = QMind.Utils.InitializeNavigationAlgo(navigationAlgorithm, worldInfo);

            AgentPosition = worldInfo.RandomCell();
            OtherPosition = worldInfo.RandomCell();
            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);

            

            
        }

        public void DoStep(bool train, WorldInfo worldInfo)
        {
            if (explorar)
            {
                //Movimiento del player (A*)
                CellInfo otherCell = QMind.Utils.MoveOther(_navigationAlgorithm, OtherPosition, AgentPosition);
                OtherPosition = otherCell;

                //Movimiento del agente (Q learning)

                float randomExploration = UnityEngine.Random.Range(0f, 1f);
                int action = UnityEngine.Random.Range(0, 4);
                CellInfo agentCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);
                int state = CalculateState(AgentPosition, OtherPosition);

                if (randomExploration <= parameters.epsilon)
                {
                    while (!agentCell.Walkable)
                    {
                        action = UnityEngine.Random.Range(0, 4);
                        agentCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);
                    }
                } 
                else
                {
                    
                    Debug.Log("NO EXPLORAMOS!");
                    action = mejorAccionQ(state);
                    agentCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);

                    //Para evitar bucles infinitos
                    if (!agentCell.Walkable)
                    {
                        while (!agentCell.Walkable)
                        {
                            action = UnityEngine.Random.Range(0, 4);
                            agentCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);
                        }
                    }
                    
                }
                //state = agentCell.x * worldInfo.WorldSize.y + agentCell.y;

                int q = getQ(AgentPosition, action, worldInfo);
                float reward = GetReward(agentCell, AgentPosition);
                float maxQ = GetMaxQ(agentCell, worldInfo);

                //Debug.Log("Estado: " + state);

                

                tableQ[state, action] = Update_rule(q, reward, maxQ);
                Debug.Log("TablaQ[" + state + ", " + action + "] = " + tableQ[state, action] + "; Iteracion: " + counter);

                //Debug.Log("Valor de q: " + tableQ[action, state]);
                AgentPosition = agentCell;


                CurrentStep = counter;
                counter += 1;

                //Debug.Log("MyTrainer: DoStep");

                if (otherCell == agentCell)
                {

                }
                    Debug.Log("csv escrito");

                    File.WriteAllLines(@"C:/Users/Javie/Documents/GitHub/DJIA_P2/Assets/Scripts/Grupo9/tableQ.csv", ToCsv(tableQ));
                
            }       
        }
        private int CalculateState(CellInfo currentPosition, CellInfo otherPosition)
        {
            return currentPosition.x * 1000 + currentPosition.y * 100 + otherPosition.x * 10 + otherPosition.y; 

        }
        public int getQ(CellInfo currentCell, int action, WorldInfo worldInfo)
        {

            int estado = (currentCell.x * worldInfo.WorldSize.y) + currentCell.y;
            float qValue = 0.0f;
            qValue = tableQ[estado, action];

            return (int) qValue;

        }
        public float GetReward(CellInfo nextCell, CellInfo currentCell)
        {
            if (nextCell.Walkable &&
                nextCell.Distance(OtherPosition, CellInfo.DistanceType.Manhattan) >
                currentCell.Distance(OtherPosition, CellInfo.DistanceType.Manhattan))
            {
                return 100.0f;
            } 
            else return 0.0f;

        }

        public float GetMaxQ(CellInfo nextCell, WorldInfo worldInfo)
        {
            
            int state = (nextCell.x * worldInfo.WorldSize.y) + nextCell.y;
            float best_q = -1000.0f;
            for (int i = 0; i < 4; i++)
            {
                if (tableQ[state, i] > best_q)
                {
                    best_q = tableQ[state, i];
                }
            }
            return best_q;
        }

        public float Update_rule(int currentQ, float reward, float maxQ)
        {
            float aux = 0.0f;
            aux = (1 - parameters.alpha) * currentQ + parameters.alpha * (reward + parameters.gamma *
           maxQ);
            return aux;
        }

        public int mejorAccionQ(int actualState)
        {
            int bestQaction = 0;
            float bestQ = -1000.0f;
            for (int i = 0; i < nCols; i++)
            {
                if (tableQ[actualState, i] >= bestQ)
                {
                    bestQ = tableQ[actualState, i];
                    bestQaction = i;
                }
            }
            return bestQaction;
        }
        public void writeCSV()
        {
            StreamWriter file = new StreamWriter(filePath);
            //my2darray  is my 2d array created.
            for (int i = 0; i < nRows; i++)
            {
                for (int j = 0; j < nCols; j++)
                {
                    file.Write(tableQ[i,j]);
                    file.Write(",");
                }
                file.Write("\n"); // go to next line
            }
            Debug.Log("CSV CREADO");
        }
        private static IEnumerable<String> ToCsv<T>(T[,] data, string separator = "/")
        {
            for (int i = 0; i < data.GetLength(0); ++i)
                yield return string.Join(separator, Enumerable
                  .Range(0, data.GetLength(1))
                  .Select(j => data[i, j])); // simplest, we don't expect ',' and '"' in the items
        }
    }
}
