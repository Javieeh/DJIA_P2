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
    public class State : MonoBehaviour
    {
        public int id;
        public bool[] walkableNeighbours; // [north, south, west, east] walkables

        // [north/aligned/south , west/aligned/east] = [1/0/-1 , 1/0/-1]
        public int[] enemyRelativePosition;

        public State(int id, bool[] walkableArray, int[] enemyPosArray)
        {
            this.id = id;
            walkableNeighbours = walkableArray;
            enemyRelativePosition = enemyPosArray;
        }
    }
    public class TableQ : MonoBehaviour
    {
        public int nRows;
        public int nCols;
        public float[,] value;
        public State[] statesArray;

        public TableQ()
        {
            // Tamaño de la tabla
            this.nRows = 4; // Acciones posibles
            this.nCols = (4 * 4) * (3 * 3); // Estados posibles
            this.value = new float[this.nRows, this.nCols]; // Matriz de valores, es decir tabla Q

        }
        public void InitializeTableQ()
        {
            
            for (int i = 0; i < this.nRows; i++)
            {
                for (int j = 0; j < this.nCols; j++)
                {
                    this.value[i, j] = 0.0f;
                }
            }

            this.statesArray = InitStatesArray(); // Array de estados
        }

        public State[] InitStatesArray()
        {
            int indice = 0;
            State[] states = new State[16 * 9]; // 16 combinaciones de walkableNeighbours y 9 combinaciones de enemyRelativePosition

            for (int i = 0; i < 16; i++)
            {
                bool[] walkableNeighbours = new bool[4];
                walkableNeighbours[0] = (i & 8) != 0; // North
                walkableNeighbours[1] = (i & 4) != 0; // South
                walkableNeighbours[2] = (i & 2) != 0; // West
                walkableNeighbours[3] = (i & 1) != 0; // East

                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        int[] enemyRelativePosition = new int[2] { j, k };
                        states[indice] = new State(indice, walkableNeighbours, enemyRelativePosition);
                        indice++;
                    }
                }
            }
            return states;
        }
    }
    public class MyTrainer : IQMindTrainer
    {
        public int CurrentEpisode { get; private set; }
        public int CurrentStep { get; private set; }
        public CellInfo AgentPosition { get; private set; }
        public CellInfo OtherPosition { get; private set; }
        public float Return { get; }
        public float ReturnAveraged { get; }
        public event EventHandler OnEpisodeStarted;
        public event EventHandler OnEpisodeFinished;

        private INavigationAlgorithm _navigationAlgorithm;
        private int counter = 0;

        QMind.QMindTrainerParams parameters;

        public TableQ tableQ;
        public bool explorar = true;

        private string filePath = "Assets/Scripts/Grupo9/tableQ.csv";

        private int numEpisode = 0;
        private int accionAnterior = -1;

        public void Initialize(QMind.QMindTrainerParams qMindTrainerParams, WorldInfo worldInfo, INavigationAlgorithm navigationAlgorithm)
        {
            this.parameters = qMindTrainerParams;
            Debug.Log("MyTrainer: initialized");

            _navigationAlgorithm = QMind.Utils.InitializeNavigationAlgo(navigationAlgorithm, worldInfo);

            AgentPosition = worldInfo.RandomCell();
            OtherPosition = worldInfo.RandomCell();
            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);

            tableQ = new TableQ();
            tableQ.InitializeTableQ();

        }

        public void DoStep(bool train, WorldInfo worldInfo)
        {
            if (explorar)
            {
                //Movimiento del agente (Q learning)
                float randomExploration = UnityEngine.Random.Range(0f, 1f);
                int action = UnityEngine.Random.Range(0, 4);
                CellInfo nextCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);

                if (randomExploration <= parameters.epsilon)
                {
                    while (!nextCell.Walkable)
                    {
                        action = UnityEngine.Random.Range(0, 4);
                        nextCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);
                    }
                }
                else
                {
                    action = mejorAccionQ(AgentPosition, worldInfo);
                    nextCell = QMind.Utils.MoveAgent(action, AgentPosition, worldInfo);



                }

                float q = getQ(AgentPosition, action, worldInfo);

                float maxQ = GetMaxQ(nextCell, worldInfo);

                float reward = GetReward(nextCell, AgentPosition);


                float newQ = Update_rule(q, reward, maxQ);
                updateTableQ(AgentPosition, worldInfo, action, newQ);

                accionAnterior = action;

                AgentPosition = nextCell;
                CellInfo otherCell = QMind.Utils.MoveOther(_navigationAlgorithm, OtherPosition, AgentPosition);
                OtherPosition = otherCell;

                CurrentStep = counter;
                //Debug.Log("MyTrainer: DoStep");
                Debug.Log("csv escrito");

                File.WriteAllLines(@"C:/Users/Javie/Documents/GitHub/DJIA_P2/Assets/Scripts/Grupo9/tableQ.csv", ToCsv(tableQ.value));

                // En el caso de que el player alcance al agente, o se llegue al máximo de pasos, se finaliza el episodio actual y se comienza uno nuevo
                if (OtherPosition == null || CurrentStep == parameters.maxSteps || OtherPosition == AgentPosition)
                {
                    OnEpisodeFinished?.Invoke(this, EventArgs.Empty);
                    NuevoEpisodio(worldInfo);
                }
                else
                {
                    counter += 1;
                }
            }
        }

        private void NuevoEpisodio(WorldInfo worldInfo)
        {
            AgentPosition = worldInfo.RandomCell();
            OtherPosition = worldInfo.RandomCell();
            counter = 0;
            CurrentStep = counter;
            numEpisode++;
            CurrentEpisode = numEpisode;
            if (numEpisode % parameters.episodesBetweenSaves == 0)
            {
                File.WriteAllLines(@"C:/Users/Javie/Documents/GitHub/DJIA_P2/Assets/Scripts/Grupo9/tableQ.csv", ToCsv(tableQ.value));
            }
            OnEpisodeStarted?.Invoke(this, EventArgs.Empty);
        }

        #region AUXILIAR METHODS
        public int mejorAccionQ(CellInfo currentCell, WorldInfo worldInfo)
        {
            //Casillas vecinas
            CellInfo north = QMind.Utils.MoveAgent(0, currentCell, worldInfo);
            CellInfo south = QMind.Utils.MoveAgent(2, currentCell, worldInfo);
            CellInfo east = QMind.Utils.MoveAgent(1, currentCell, worldInfo);
            CellInfo west = QMind.Utils.MoveAgent(3, currentCell, worldInfo);

            bool[] tempWalkableArray = new bool[] {
                north.Walkable, east.Walkable, south.Walkable, west.Walkable
            };

            //Posicion relativa del enemigo
            int[] tempEnemyRelativePosition = new int[2];
            if (OtherPosition.y > currentCell.y)
            {
                tempEnemyRelativePosition[0] = 1;
            }
            if (OtherPosition.y == currentCell.y)
            {
                tempEnemyRelativePosition[0] = 0;
            }
            if (OtherPosition.y < currentCell.y)
            {
                tempEnemyRelativePosition[0] = -1;
            }

            if (OtherPosition.x > currentCell.x)
            {
                tempEnemyRelativePosition[1] = 1;
            }
            if (OtherPosition.x == currentCell.x)
            {
                tempEnemyRelativePosition[1] = 0;
            }
            if (OtherPosition.x < currentCell.x)
            {
                tempEnemyRelativePosition[1] = -1;
            }

            int indice = 0;
            //Buscamos el estado que corresponda a la casilla vecina y posicion del enemigo relativa calculada
            for (int i = 0; i < tableQ.statesArray.Length; i++)
            {
                if (compararArraysBooleanos(tempWalkableArray, tableQ.statesArray[i].walkableNeighbours)
                                                &&
                    compararArraysInt(tempEnemyRelativePosition, tableQ.statesArray[i].enemyRelativePosition))
                {
                    indice = i;
                }
            }

            int bestAction = 0;
            float best_q = -1000f;

            for (int actualAction = 0; actualAction < tableQ.nRows; actualAction++)
            {
                if (tableQ.value[actualAction, indice] > best_q)
                {
                    bestAction = actualAction;
                    best_q = tableQ.value[bestAction, indice];
                }
            }

            return bestAction;
        }

        public float getQ(CellInfo currentCell, int action, WorldInfo worldInfo)
        {

            //Casillas vecinas
            CellInfo north = QMind.Utils.MoveAgent(0, currentCell, worldInfo);
            CellInfo south = QMind.Utils.MoveAgent(2, currentCell, worldInfo);
            CellInfo east = QMind.Utils.MoveAgent(1, currentCell, worldInfo);
            CellInfo west = QMind.Utils.MoveAgent(3, currentCell, worldInfo);

            bool[] tempWalkableArray = new bool[] {
                north.Walkable, east.Walkable, south.Walkable, west.Walkable
            };

            //Posicion relativa del enemigo
            int[] tempEnemyRelativePosition = new int[2];
            if (OtherPosition.y > currentCell.y)
            {
                tempEnemyRelativePosition[0] = 1;
            }
            if (OtherPosition.y == currentCell.y)
            {
                tempEnemyRelativePosition[0] = 0;
            }
            if (OtherPosition.y < currentCell.y)
            {
                tempEnemyRelativePosition[0] = -1;
            }

            if (OtherPosition.x > currentCell.x)
            {
                tempEnemyRelativePosition[1] = 1;
            }
            if (OtherPosition.x == currentCell.x)
            {
                tempEnemyRelativePosition[1] = 0;
            }
            if (OtherPosition.x < currentCell.x)
            {
                tempEnemyRelativePosition[1] = -1;
            }

            //Buscamos el estado que corresponda a la casilla vecina y posicion del enemigo relativa calculada
            for (int i = 0; i < tableQ.statesArray.Length; i++)
            {
                if (compararArraysBooleanos(tempWalkableArray, tableQ.statesArray[i].walkableNeighbours)
                                                &&
                    compararArraysInt(tempEnemyRelativePosition, tableQ.statesArray[i].enemyRelativePosition))
                {
                    return tableQ.value[action, i];
                }
            }

            Debug.Log("NO SE HA ENCONTRADO EL ESTADO");
            return 0;


        }

        public float GetMaxQ(CellInfo currentCell, WorldInfo worldInfo)
        {

            //Casillas vecinas
            CellInfo north = QMind.Utils.MoveAgent(0, currentCell, worldInfo);
            CellInfo south = QMind.Utils.MoveAgent(2, currentCell, worldInfo);
            CellInfo east = QMind.Utils.MoveAgent(1, currentCell, worldInfo);
            CellInfo west = QMind.Utils.MoveAgent(3, currentCell, worldInfo);

            bool[] tempWalkableArray = new bool[] {
                north.Walkable, east.Walkable, south.Walkable, west.Walkable
            };

            //Posicion relativa del enemigo
            int[] tempEnemyRelativePosition = new int[2];
            if (OtherPosition.y > currentCell.y)
            {
                tempEnemyRelativePosition[0] = 1;
            }
            if (OtherPosition.y == currentCell.y)
            {
                tempEnemyRelativePosition[0] = 0;
            }
            if (OtherPosition.y < currentCell.y)
            {
                tempEnemyRelativePosition[0] = -1;
            }

            if (OtherPosition.x > currentCell.x)
            {
                tempEnemyRelativePosition[1] = 1;
            }
            if (OtherPosition.x == currentCell.x)
            {
                tempEnemyRelativePosition[1] = 0;
            }
            if (OtherPosition.x < currentCell.x)
            {
                tempEnemyRelativePosition[1] = -1;
            }

            int indice = 0;
            //Buscamos el estado que corresponda a la casilla vecina y posicion del enemigo relativa calculada
            for (int i = 0; i < tableQ.statesArray.Length; i++)
            {
                if (compararArraysBooleanos(tempWalkableArray, tableQ.statesArray[i].walkableNeighbours)
                                                &&
                    compararArraysInt(tempEnemyRelativePosition, tableQ.statesArray[i].enemyRelativePosition))
                {
                    indice = i;
                }
            }

            float best_q = -1000f;

            for (int actualAction = 0; actualAction < tableQ.nRows; actualAction++)
            {
                if (tableQ.value[actualAction, indice] > best_q)
                {
                    best_q = tableQ.value[actualAction, indice];
                }
            }

            return best_q;
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

        public float Update_rule(float currentQ, float reward, float maxQ)
        {
            float aux = 0.0f;
            aux = (1 - parameters.alpha) * currentQ + parameters.alpha * (reward + parameters.gamma *
           maxQ);
            return aux;
        }

        public void updateTableQ(CellInfo currentCell, WorldInfo worldInfo, int accion, float newQ)
        {
            //Casillas vecinas
            CellInfo north = QMind.Utils.MoveAgent(0, currentCell, worldInfo);
            CellInfo south = QMind.Utils.MoveAgent(2, currentCell, worldInfo);
            CellInfo east = QMind.Utils.MoveAgent(1, currentCell, worldInfo);
            CellInfo west = QMind.Utils.MoveAgent(3, currentCell, worldInfo);

            bool[] tempWalkableArray = new bool[] {
                north.Walkable, east.Walkable, south.Walkable, west.Walkable
            };

            //Posicion relativa del enemigo
            int[] tempEnemyRelativePosition = new int[2];
            if (OtherPosition.y > currentCell.y)
            {
                tempEnemyRelativePosition[0] = 1;
            }
            if (OtherPosition.y == currentCell.y)
            {
                tempEnemyRelativePosition[0] = 0;
            }
            if (OtherPosition.y < currentCell.y)
            {
                tempEnemyRelativePosition[0] = -1;
            }

            if (OtherPosition.x > currentCell.x)
            {
                tempEnemyRelativePosition[1] = 1;
            }
            if (OtherPosition.x == currentCell.x)
            {
                tempEnemyRelativePosition[1] = 0;
            }
            if (OtherPosition.x < currentCell.x)
            {
                tempEnemyRelativePosition[1] = -1;
            }

            int indice = 0;
            //Buscamos el estado que corresponda a la casilla vecina y posicion del enemigo relativa calculada
            for (int i = 0; i < tableQ.statesArray.Length; i++)
            {
                if (compararArraysBooleanos(tempWalkableArray, tableQ.statesArray[i].walkableNeighbours)
                                                &&
                    compararArraysInt(tempEnemyRelativePosition, tableQ.statesArray[i].enemyRelativePosition))
                {
                    indice = i;
                }
            }

            tableQ.value[accion, indice] = newQ;
        }

        #endregion

        #region CSV
        public void writeCSV()
        {
            StreamWriter file = new StreamWriter(filePath);
            //my2darray  is my 2d array created.
            for (int i = 0; i < tableQ.nRows; i++)
            {
                for (int j = 0; j < tableQ.nCols; j++)
                {
                    file.Write(tableQ.value[i, j]);
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
        #endregion

        public static bool compararArraysBooleanos(bool[] array1, bool[] array2)
        {
            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }

            return true;
        }

        public static bool compararArraysInt(int[] array1, int[] array2)
        {
            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (array1[i] != array2[i])
                    return false;
            }

            return true;
        }
    }


}
