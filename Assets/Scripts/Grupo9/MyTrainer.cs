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

        public void Initialize(QMind.QMindTrainerParams qMindTrainerParams, WorldInfo worldInfo, INavigationAlgorithm navigationAlgorithm)
        {
            this.nRows = 4; //Numero de acciones posibles (izquierda, derecha, arriba, abajo)
            this.nCols = worldInfo.WorldSize.x * worldInfo.WorldSize.y; //Cálculo del grid 
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
                int state = agentCell.x * worldInfo.WorldSize.y + agentCell.y;
                CellInfo agentcell;

                if (randomExploration <= parameters.epsilon)
                {
                    while (!agentCell.Walkable)
                    {
                        action = UnityEngine.Random.Range(0, 4);
                        agentCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);
                    }
                } else
                {
                    Debug.Log("NO EXPLORAMOS!");
                    action = mejorAccionQ(AgentPosition);
                    agentcell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);
                }
                state = agentCell.x * worldInfo.WorldSize.y + agentCell.y;


                float q = getQ(AgentPosition, action, worldInfo);
                float reward = GetReward(agentCell, AgentPosition);
                float maxQ = GetMaxQ(agentCell, worldInfo);

                Debug.Log("Estado: " + state);
                if (tableQ[action, state] == 0.0f)
                {
                    tableQ[action, state] = Update_rule(q, reward, maxQ);
                }
                Debug.Log("Valor de q: " + tableQ[action, state]);
                AgentPosition = agentCell;


                CurrentStep = counter;
                counter += 1;

                //Debug.Log("MyTrainer: DoStep");

                if (OtherPosition == AgentPosition)
                {
                    File.WriteAllLines(@"C:/Users/Javie/Documents/GitHub/DJIA_P2/Assets/Scripts/Grupo9/tableQ.csv", ToCsv(tableQ));
                }
            }       
        }

        public float getQ(CellInfo currentCell, int action, WorldInfo worldInfo)
        {
            //float maxQ = GetMaxQ();
            //float reward = GetReward();
            //float nextQ = (1 - parameters.alpha) * q + parameters.alpha * (reward + parameters.gamma*maxQ);
            int estado = (currentCell.x * worldInfo.WorldSize.y) + currentCell.y;
            float qValue = 0.0f;
            qValue = tableQ[action, estado];

            return qValue;

        }
        public float GetReward(CellInfo nextCell, CellInfo currentCell)
        {
            if (nextCell.Walkable &&
                nextCell.Distance(OtherPosition, CellInfo.DistanceType.Manhattan) > 
                currentCell.Distance(OtherPosition, CellInfo.DistanceType.Manhattan))
            {
                return 100.0f;
            }
            else return -1.0f;
        }

        public float GetMaxQ(CellInfo nextCell, WorldInfo worldInfo)
        {
            
            int state = (nextCell.x * worldInfo.WorldSize.y) + nextCell.y;
            float best_q = -1000.0f;
            for (int i = 0; i < 4; i++)
            {
                if (tableQ[i, state] > best_q)
                {
                    best_q = tableQ[i, state];
                }
            }
            return best_q;
        }

        public float Update_rule(float currentQ, float reward, float maxQ)
        {
            float aux = 0.0f;
            aux = (1 - parameters.alpha) * currentQ + parameters.alpha * (reward + parameters.gamma *
           maxQ);
            return aux;
        }

        public int mejorAccionQ(CellInfo actualState)
        {
            int bestQaction = 0;
            float bestQ = -1000.0f;
            for (int i = 0; i < nRows; i++)
            {
                if (tableQ[i, actualState.x*actualState.y] >= bestQ)
                {
                    bestQ = tableQ[i, actualState.x * actualState.y];
                    bestQaction = i;
                }
            }
            return bestQaction;
        }
        public void writeCSV()
        {
            StreamWriter file = new StreamWriter("C:/Users/Javie/Documents/GitHub/DJIA_P2/Assets/Scripts/Grupo9/tableQ.csv");
            //my2darray  is my 2d array created.
            for (int i = 0; i < nRows; i++)
            {
                for (int j = 0; j < nCols; j++)
                {
                    file.Write(tableQ[i,j]);
                    file.Write("\t");
                }
                file.Write("\n"); // go to next line
            }
            Debug.Log("CSV CREADO");
        }
        private static IEnumerable<String> ToCsv<T>(T[,] data, string separator = ",")
        {
            for (int i = 0; i < data.GetLength(0); ++i)
                yield return string.Join(separator, Enumerable
                  .Range(0, data.GetLength(1))
                  .Select(j => data[i, j])); // simplest, we don't expect ',' and '"' in the items
        }
    }
}
