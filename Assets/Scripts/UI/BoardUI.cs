using System.Collections;
using UnityEngine;

namespace Chess.Game
{
    public class BoardUI : MonoBehaviour
    {
        public PieceTheme pieceTheme;
        public BoardTheme boardTheme;
        public bool showLegalMoves;

        public GameManager gameManager;

        public bool isIA = false;

        public bool whiteIsBottom = true;

        public MeshRenderer[,] squareRenderers;
        public GameObject[,] squarePieceRenderers;
        Move lastMadeMove;
        MoveGenerator moveGenerator;

        public AudioSource source;

        const float pieceDepth = -0.1f;
        const float pieceDragDepth = -0.2f;

        public int selectedSound;

        public AISettings aiSettings;

        void Awake()
        {
            changeDefaultSound();
            moveGenerator = new MoveGenerator();
            CreateBoardUI();
        }

        public void changeDefaultSound()
        {
            int random = Random.Range(0, aiSettings.audios.Length);
            selectedSound = random;
            source.clip = aiSettings.audios[random];
        }

        public void HighlightLegalMoves(Board board, Coord fromSquare)
        {
            if (showLegalMoves)
            {

                var moves = moveGenerator.GenerateMoves(board);

                for (int i = 0; i < moves.Count; i++)
                {
                    Move move = moves[i];
                    if (move.StartSquare == BoardRepresentation.IndexFromCoord(fromSquare))
                    {
                        Coord coord = BoardRepresentation.CoordFromIndex(move.TargetSquare);
                        SetSquareColour(coord, boardTheme.lightSquares.legal, boardTheme.darkSquares.legal);
                    }
                }
            }
        }

        public void DragPiece(Coord pieceCoord, Vector2 mousePos)
        {
            squarePieceRenderers[pieceCoord.fileIndex, pieceCoord.rankIndex].transform.position = new Vector3(mousePos.x, mousePos.y, pieceDragDepth);
        }

        public void ResetPiecePosition(Coord pieceCoord)
        {
            Vector3 pos = PositionFromCoord(pieceCoord.fileIndex, pieceCoord.rankIndex, pieceDepth);
            squarePieceRenderers[pieceCoord.fileIndex, pieceCoord.rankIndex].transform.position = pos;
        }

        public void SelectSquare(Coord coord)
        {
            SetSquareColour(coord, boardTheme.lightSquares.selected, boardTheme.darkSquares.selected);
        }

        public void DeselectSquare(Coord coord)
        {
            //BoardTheme.SquareColours colours = (coord.IsLightSquare ()) ? boardTheme.lightSquares : boardTheme.darkSquares;
            //squareMaterials[coord.file, coord.rank].color = colours.normal;
            ResetSquareColours();
        }

        public bool TryGetSquareUnderMouse(Vector2 mouseWorld, out Coord selectedCoord)
        {
            int file = (int)(mouseWorld.x + 4);
            int rank = (int)(mouseWorld.y + 4);
            if (!whiteIsBottom)
            {
                file = 7 - file;
                rank = 7 - rank;
            }
            selectedCoord = new Coord(file, rank);
            return file >= 0 && file < 8 && rank >= 0 && rank < 8;
        }

        public void UpdatePosition(Board board)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    Coord coord = new Coord(file, rank);
                    int piece = board.Square[BoardRepresentation.IndexFromCoord(coord.fileIndex, coord.rankIndex)];

                    if (pieceTheme.GetPieceSprite(piece) == null)
                    {
						DestroyImmediate(squarePieceRenderers[file, rank]);
                        continue;
                    }

                    DestroyImmediate(squarePieceRenderers[file, rank]);
                    GameObject a = Instantiate(pieceTheme.GetPieceSprite(piece), new Vector3(0, 0, 0), Quaternion.Euler(0, 90, -90));
                    squarePieceRenderers[file, rank] = a;

                    //Debug.Log("creating new object");


                    if (isIA)
                    {
                        //squarePieceRenderers[file, rank].color = boardTheme.lightSquares.normal;
                    }
                    else
                    {
                        //squarePieceRenderers[file, rank].color = Color.white;
                    }


                    squarePieceRenderers[file, rank].transform.position = PositionFromCoord(file, rank, pieceDepth);
                }
            }

        }

        public void OnMoveMade(Board board, Move move, bool animate = false)
        {
            lastMadeMove = move;

            if (animate)
            {
                source.volume = 1 - Random.Range(0, 0.5f);
                source.pitch = 1 - Random.Range(-0.1f, 0.1f);
                StartCoroutine(AnimateMove(move, board));
            }
            else
            {
                UpdatePosition(board);
                ResetSquareColours();
            }
        }

        IEnumerator AnimateMove(Move move, Board board)
        {
            float t = 0;
            const float moveAnimDuration = 0.1f;
            Coord startCoord = BoardRepresentation.CoordFromIndex(move.StartSquare);
            Coord targetCoord = BoardRepresentation.CoordFromIndex(move.TargetSquare);
            Transform pieceT = squarePieceRenderers[startCoord.fileIndex, startCoord.rankIndex].transform;
            Vector3 startPos = PositionFromCoord(startCoord);
            Vector3 targetPos = PositionFromCoord(targetCoord);
            //SetSquareColour (BoardRepresentation.CoordFromIndex (move.StartSquare), trans, new Color(0.15f,0.15f,0.15f));

            while (t <= 1)
            {
                yield return null;
                t += Time.deltaTime * 1 / moveAnimDuration;
                pieceT.position = Vector3.Lerp(startPos, targetPos, t);
            }

            source.Play();



            switch (move.MoveFlag)
            {
                case Move.Flag.Castling:
                    DestroyImmediate(pieceT.gameObject);

					board.Square[BoardRepresentation.IndexFromCoord(startCoord.fileIndex, startCoord.rankIndex)] = 0;
					//board.Square[BoardRepresentation.IndexFromCoord(targetCoord.fileIndex, targetCoord.rankIndex)] = 0;


					//board.Square[BoardRepresentation.IndexFromCoord(startCoord.fileIndex, startCoord.rankIndex)] = null;
					//squarePieceRenderers[targetCoord.fileIndex, targetCoord.rankIndex] = null;

                    break;
                default:
                    DestroyImmediate(pieceT.gameObject);
                    break;

            }

            UpdatePosition(board);
            ResetSquareColours();

        }

        void HighlightMove(Move move)
        {
            Color trans = boardTheme.lightSquares.legal;
            trans.a = 0.5f;

            //SetSquareColour (BoardRepresentation.CoordFromIndex (move.StartSquare), trans, trans);
            //SetSquareColour (BoardRepresentation.CoordFromIndex (move.TargetSquare), trans, trans);
        }

        void CreateBoardUI()
        {
            //foreach(Transform child in this.transform)
            //{
            //    DestroyImmediate(child.gameObject);
            //}
            Debug.Log("Criando um novo board");

            Shader squareShader = Shader.Find("Unlit/Color");
            squareRenderers = new MeshRenderer[8, 8];
            squarePieceRenderers = new GameObject[8, 8];

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    GameObject a = GameObject.Find(BoardRepresentation.SquareNameFromCoordinate(file, rank));
                    if(a){
                        DestroyImmediate(a);
                    }
                    
                    // Create square
                    Transform square = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
                    square.parent = transform;
                    square.name = BoardRepresentation.SquareNameFromCoordinate(file, rank);
                    square.position = PositionFromCoord(file, rank, 0);
                    Material squareMaterial = new Material(squareShader);

                    


                    squareRenderers[file, rank] = square.gameObject.GetComponent<MeshRenderer>();
                    squareRenderers[file, rank].material = squareMaterial;


                    // Create piece sprite renderer for current square
                    GameObject pieceRenderer = new GameObject("Piece");
                    pieceRenderer.transform.parent = square;
                    pieceRenderer.transform.position = PositionFromCoord(file, rank, pieceDepth);
                    pieceRenderer.transform.localScale = Vector3.one * 100 / (2000 / 6f);



                    squarePieceRenderers[file, rank] = pieceRenderer;
                }
            }

            ResetSquareColours();
        }

        void ResetSquarePositions()
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (file == 0 && rank == 0)
                    {
                        //Debug.Log (squarePieceRenderers[file, rank].gameObject.name + "  " + PositionFromCoord (file, rank, pieceDepth));
                    }
                    //squarePieceRenderers[file, rank].transform.position = PositionFromCoord (file, rank, pieceDepth);
                    squareRenderers[file, rank].transform.position = PositionFromCoord(file, rank, 0);

                    if (squarePieceRenderers[file, rank])
                    {
                        DestroyImmediate(squarePieceRenderers[file, rank]);
                    }
                }
            }

            if (!lastMadeMove.IsInvalid)
            {
                HighlightMove(lastMadeMove);
            }

            CreateBoardUI();
        }

        public void SetPerspective(bool whitePOV)
        {
            whiteIsBottom = whitePOV;
            ResetSquarePositions();

        }

        public void ResetSquareColours(bool highlight = true)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    SetSquareColour(new Coord(file, rank), boardTheme.lightSquares.normal, boardTheme.darkSquares.normal);
                }
            }
            if (highlight)
            {
                if (!lastMadeMove.IsInvalid)
                {
                    HighlightMove(lastMadeMove);
                }
            }
        }

        void SetSquareColour(Coord square, Color lightCol, Color darkCol)
        {
            squareRenderers[square.fileIndex, square.rankIndex].material.color = (square.IsLightSquare()) ? lightCol : darkCol;
        }

        public Vector3 PositionFromCoord(int file, int rank, float depth = 0)
        {
            if (whiteIsBottom)
            {
                return new Vector3(-3.5f + file, -3.5f + rank, depth - 0.02f);
            }
            return new Vector3(-3.5f + 7 - file, 7 - rank - 3.5f, depth - 0.02f);

        }

        public Vector3 PositionFromCoord(Coord coord, float depth = 0)
        {
            return PositionFromCoord(coord.fileIndex, coord.rankIndex, depth);
        }

    }
}