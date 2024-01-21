using Grupo9;
using NavigationDJIA.World;
using QMind.Interfaces;
using System.IO;
using UnityEngine;

namespace GrupoP
{
    public class MyTester : IQMind
    {
        private WorldInfo _worldInfo;
        private string filePath = "C:/Users/Javie/Documents/GitHub/DJIA_P2/Assets/Scripts/Grupo9/tableQ.csv";

        //Tabla Q
        public float[,] tableQaux { get; set; }
        public float[,] tableQ { get; set; }

        private int nRows { get; set; }
        private int nCols { get; set; }

        public void Initialize(WorldInfo worldInfo)
        {
            _worldInfo = worldInfo;

            this.nRows = worldInfo.WorldSize.x * 1000 + worldInfo.WorldSize.y * 100 + worldInfo.WorldSize.x * 10 + worldInfo.WorldSize.y; //Cálculo del grid 
            this.nCols = 4; //Numero de acciones posibles (izquierda, derecha, arriba, abajo)
            this.tableQaux = new float[nRows, nCols];

            this.tableQ = LoadQTable(this.tableQaux);

            for (int i = 0; i < this.nRows; i++)
            {
                for (int j = 0; j < this.nCols; j++)
                {
                    this.tableQ[i, j] = 0.0f;
                    Debug.Log(this.tableQ[i, j]);
                }
            }
        }

        public CellInfo GetNextStep(CellInfo currentPosition, CellInfo otherPosition)
        {
            int state = CalculateState(currentPosition, otherPosition);
            int action = GetAction(state);
            CellInfo agentCell = QMind.Utils.MoveAgent(action, currentPosition, _worldInfo);

            while (!agentCell.Walkable)
            {
                action = GetAction(Random.Range(0, 4));
                agentCell = QMind.Utils.MoveAgent(action, currentPosition, _worldInfo);

            }
            Debug.Log("Action = " + action);

            return agentCell;
        }

        private int GetAction(int state)
        {
            int bestQaction = 0;
            float bestQ = -1000.0f;
            for (int i = 0; i < nCols; i++)
            {
                if (tableQ[state, i] >= bestQ)
                {
                    bestQ = tableQ[state, i];
                    bestQaction = i;
                }
            }
            return bestQaction;
        }

        private float[,] LoadQTable(float[,] table)
        {
            if (File.Exists(filePath))
            {
                // Lee todas las líneas del archivo CSV
                string[] lines = File.ReadAllLines(filePath);

                for (int i = 0; i < lines.Length; i++)
                {
                    // Divide cada línea en valores usando la coma como separador
                    string[] values = lines[i].Split(',');

                    for (int j = 0; j < values.Length && j < nCols; j++)
                    {
                        // Convierte cada valor a float y almacénalo en la matriz
                        float parsedValue;
                        if (float.TryParse(values[j], out parsedValue))
                        {
                            table[i, j] = parsedValue;
                        }
                        else
                        {
                            Debug.LogError("Error al analizar el valor en la posición [" + i + ", " + j + "]");
                        }
                    }
                }

                // La matriz bidimensional 'table' ahora contiene los valores del CSV
                Debug.Log("CSV leído exitosamente");

                // Puedes asignar 'table' a 'tableQ' si es necesario
                tableQ = table;
                return tableQ;
            }
            else
            {
                Debug.LogError("El archivo CSV no existe en la ruta: " + filePath);
            }
            return null;
        }
        private int CalculateState(CellInfo currentPosition, CellInfo otherPosition)
        {
            return currentPosition.x * 1000 + currentPosition.y * 100 + otherPosition.x * 10 + otherPosition.y; 

        }
    }
}
