using Grupo9;
using NavigationDJIA.World;
using QMind.Interfaces;
using System;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

namespace Grupo9
{
    public class MyTester : IQMind
    {
        private WorldInfo worldInfo;
        
        private float[,] _tablaQ; //Valores de la tabla Q
        Grupo9.State[] states = new Grupo9.State[16 * 9]; //array de estados

        //tamaño de la tabla Q
        private int nRows = 4;
        private int nCols = (4*4)* (3*3);

        public void Initialize(WorldInfo worldInfo)
        {
            this.worldInfo = worldInfo;
            InitializeStates(); //inicializamos array de estados
            LoadQTable();

            
        }

        public CellInfo GetNextStep(CellInfo currentPosition, CellInfo otherPosition)
        {

            Grupo9.State state = CalculateState(currentPosition, otherPosition, worldInfo); //se calcula el estado
            int action = GetAction(state); //se calcula la mejor accion

            CellInfo agentCell = QMind.Utils.MoveAgent(action, currentPosition, worldInfo); //se calcula la celda

            return agentCell;
        }

        private int GetAction(Grupo9.State state)
        {
            //Se deuvel la mejor acción según el valor de Q
            int bestQaction = 0;
            float bestQ = -1000.0f;
            for (int i = 0; i < nRows; i++)
            {
                if (_tablaQ[i, state.id] >= bestQ)
                {
                    bestQ = _tablaQ[i, state.id];
                    bestQaction = i;
                }
            }
            return bestQaction;
        }

        private void InitializeStates()
        {
            int indice = 0;

             // 16 combinaciones de walkableNeighbours y 9 combinaciones de enemyRelativePosition
            for (int i = 0; i < 16; i++)
            {
                bool[] walkableNeighbours = new bool[4];
                walkableNeighbours[0] = (i & 8) != 0; // North
                walkableNeighbours[2] = (i & 4) != 0; // East
                walkableNeighbours[1] = (i & 2) != 0; // South
                walkableNeighbours[3] = (i & 1) != 0; // West

                for (int j = -1; j <= 1; j++)
                {
                    for (int k = -1; k <= 1; k++)
                    {
                        int[] enemyRelativePosition = new int[2] { j, k };
                        states[indice] = new Grupo9.State(indice, walkableNeighbours, enemyRelativePosition);
                        indice++;
                    }
                }
            }
        }
        private void LoadQTable()
        {
            string filePath = @"Assets/Scripts/Grupo9/tableQ.csv"; //Se busca la tablaQ
            StreamReader reader;
            if (File.Exists(filePath))
            {
                reader = new StreamReader(File.OpenRead(filePath));
                _tablaQ = new float[nRows, nCols];
                int contador = 0;
                while (!reader.EndOfStream && contador < nRows)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');
                    for (int i = 0; i < values.Length; i++)
                    {
                        _tablaQ[contador, i] = (float)Convert.ToDouble(values[i]);
                    }
                    contador++;
                }
            }
        }

        private Grupo9.State CalculateState(CellInfo currentCell, CellInfo OtherPosition, WorldInfo worldInfo)
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
            for (int i = 0; i < states.Length; i++)
            {
                if (compararArraysBooleanos(tempWalkableArray, states[i].walkableNeighbours)
                                                &&
                    compararArraysInt(tempEnemyRelativePosition, states[i].enemyRelativePosition))
                {
                    return states[i]; //Se devuelve el estado
                }
            }
            return null;

        }

        //Métodos para comparar los arrays
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
