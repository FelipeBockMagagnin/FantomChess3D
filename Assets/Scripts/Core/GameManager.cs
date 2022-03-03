using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Recorder;
using System.IO;
using System.Xml.Serialization;


namespace Chess.Game
{
    public class GameManager : MonoBehaviour
    {

        public BoardTheme boardTheme;

        public TextAsset jsonPGN;

        public enum Result { Playing, WhiteIsMated, BlackIsMated, Stalemate, Repetition, FiftyMoveRule, InsufficientMaterial }

        public event System.Action onPositionLoaded;
        public event System.Action<Move> onMoveMade;

        public enum PlayerType { Human, AI }

        public bool loadCustomPosition;
        public string customPosition = "1rbq1r1k/2pp2pp/p1n3p1/2b1p3/R3P3/1BP2N2/1P3PPP/1NBQ1RK1 w - - 0 1";

        public PlayerType whitePlayerType;
        public PlayerType blackPlayerType;
        public AISettings aiSettings;
        public Color[] colors;

        public bool useClocks;
        public Clock whiteClock;
        public Clock blackClock;
        public TMPro.TMP_Text aiDiagnosticsUI;
        public TMPro.TMP_Text resultUI;

        Result gameResult;

        Player whitePlayer;
        Player blackPlayer;
        Player playerToMove;
        BoardUI boardUI;
        int moveIndex;

        string sound = "Sound 1";

        public ulong zobristDebug;
        public Board board { get; private set; }
        Board searchBoard; // Duplicate version of board used for ai search

        string gameName = "none";

        int count = 0;
        int champGamesCount = 0;
        GameCollection allPgnGames = new GameCollection();

        public RecorderController recorder;
        public List<Move> gameMoves;

        private void StartRecorder()
        {
            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var TestRecorderController = new RecorderController(controllerSettings);

            var videoRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            videoRecorder.name = "My Video Recorder";
            videoRecorder.Enabled = true;
            videoRecorder.VideoBitRateMode = UnityEditor.VideoBitrateMode.High;

            videoRecorder.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            videoRecorder.AudioInputSettings.PreserveAudio = true;
            videoRecorder.OutputFile = "Recordings/" + count;

            controllerSettings.AddRecorderSettings(videoRecorder);
            controllerSettings.FrameRate = 30;

            RecorderOptions.VerboseMode = false;
            TestRecorderController.PrepareRecording();
            recorder = TestRecorderController;
            TestRecorderController.StartRecording();
        }

        void Start()
        {
            Application.targetFrameRate = 30;

            if (useClocks)
            {
                whiteClock.isTurnToMove = false;
                blackClock.isTurnToMove = false;
            }


            boardUI = FindObjectOfType<BoardUI>();
            gameMoves = new List<Move>();
            board = new Board();
            searchBoard = new Board();
            aiSettings.diagnostics = new Search.SearchDiagnostics();

            NewGame(whitePlayerType, blackPlayerType);

        

            //PlayPgnGames();
            //StartCoroutine(NewComputerVersusComputerGame(true));

            StartCoroutine(StartPlayAllGames());
        }


        public int type; // 0 -> normal, 1 -> champ

        IEnumerator StartPlayAllGames(){
            StringReader reader = new StringReader(jsonPGN.text);
            XmlSerializer serializer = new XmlSerializer(typeof(GameCollection));
            allPgnGames = serializer.Deserialize(reader) as GameCollection;

            yield return new WaitForSeconds(2f);
            StartCoroutine(PlayAllGames());
        }

        IEnumerator PlayAllGames(){
            yield return new WaitForSeconds(0.5f);
            
            //Decidir se é jogo normal ou campeonato
            int d10 = Random.Range(0, 10);


            if(d10 == 1 && champGamesCount < 55){
                //Jogo campeonato
                type = 1;

                //change colors
                boardTheme.lightSquares.normal = new Color(0.94f, 0.76f, 0.5f);
                boardTheme.darkSquares.normal = new Color(0.42f, 0.24f, 0.09f);

                boardUI.isIA = false;

                //restart
                restart();

                //Load Moves
                gameMoves = PGNLoader.MovesFromPGN(allPgnGames.game[champGamesCount].pgn).ToList();
            }
            else{
                //Jogo normal
                type = 0;

                //change colors
                float[] colors = new float[] { Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f) };
                colors[Random.Range(0, 2)] = 1f;
                boardTheme.lightSquares.normal = new Color(
                    colors[0],
                    colors[1],
                    colors[2]
                );

                boardTheme.darkSquares.normal = Color.black;

                //restart positions\
                Debug.Log("restating positions");
                boardUI.isIA = true;
                gameMoves.Clear();
                board.LoadStartPosition();
                boardUI.SetPerspective(true);
                boardUI.ResetSquareColours();
                boardUI.UpdatePosition(board);
            }

            yield return new WaitForSeconds(1f);

            StartRecorder();

            yield return new WaitForSeconds(0.5f);

            if(type == 0){
                //normal
                NewGame(PlayerType.AI, PlayerType.AI);

                //Will call 'awaitStopRecord'
            }
            else{
                //champ
                while (moveIndex < gameMoves.Count())
                {
                    board.MakeMove(gameMoves[moveIndex]);
                    boardUI.OnMoveMade(board, gameMoves[moveIndex], true);
                    moveIndex++;
                    yield return new WaitForSeconds(float.Parse("0," + aiSettings.searchTimeMillis));
                }

                StartCoroutine(awaitStopRecord(allPgnGames.game[champGamesCount].winner == "white" ? Result.BlackIsMated : Result.WhiteIsMated));
            }
        }

        IEnumerator awaitStopRecord(Result result)
        {
            //Preenche infos uteis 
            if(type == 0){
                gameName = "none";
            } else{
                gameName = allPgnGames.game[champGamesCount].name;
                champGamesCount++;
            }            
            
            sound = "Sound " + boardUI.selectedSound;

            yield return new WaitForSeconds(1f);

            recorder.StopRecording();
            ExportGame(result);

            count++;
            
            yield return new WaitForSeconds(1f);

            Debug.Log("stopping game");

            StartCoroutine(PlayAllGames());
        }

        void restart(){
            Debug.Log("restarting");
            boardUI = FindObjectOfType<BoardUI>();
            moveIndex = 0;
            gameMoves = new List<Move>();
            board = new Board();

            boardUI.changeDefaultSound();
            searchBoard = new Board();
            aiSettings.diagnostics = new Search.SearchDiagnostics();

            NewGame(whitePlayerType, blackPlayerType);
        }

        void PlayPgnGames()
        {
            gameResult = Result.WhiteIsMated;

            StartCoroutine(runGameView());
        }

        IEnumerator runGameView()
        {
            yield return new WaitForSeconds(2f);

            boardTheme.lightSquares.normal = new Color(0.94f, 0.76f, 0.5f);
            boardTheme.darkSquares.normal = new Color(0.42f, 0.24f, 0.09f);

            StringReader reader = new StringReader(jsonPGN.text);
            XmlSerializer serializer = new XmlSerializer(typeof(GameCollection));
            GameCollection xml = serializer.Deserialize(reader) as GameCollection;

            foreach (Game game in xml.game)
            {
                Debug.Log(count + " playing now " + game.name + "pgn: " + game.pgn );
                StartRecorder();

                restart();
                gameMoves = PGNLoader.MovesFromPGN(game.pgn).ToList();

                yield return new WaitForSeconds(0.5f);

                while (moveIndex < gameMoves.Count())
                {
                    board.MakeMove(gameMoves[moveIndex]);
                    boardUI.OnMoveMade(board, gameMoves[moveIndex], true);
                    moveIndex++;
                    yield return new WaitForSeconds(float.Parse("0," + aiSettings.searchTimeMillis));
                }

                yield return new WaitForSeconds(1);

                gameName = game.name;
                sound = "Sound " + boardUI.selectedSound;
                count++;

                recorder.StopRecording();
                ExportGame(game.winner == "white" ? Result.BlackIsMated : Result.WhiteIsMated );
                yield return new WaitForSeconds(0.5f);
                moveIndex = 0;
                gameMoves = new List<Move>();
            }     
        }

        void Update()
        {
            zobristDebug = board.ZobristKey;

            if (gameResult == Result.Playing)
            {
                LogAIDiagnostics();

                playerToMove.Update();

                if (useClocks)
                {
                    whiteClock.isTurnToMove = board.WhiteToMove;
                    blackClock.isTurnToMove = !board.WhiteToMove;
                }
            }
        }

        void OnMoveChosen(Move move)
        {
            bool animateMove = playerToMove is AIPlayer;
            board.MakeMove(move);
            searchBoard.MakeMove(move);

            gameMoves.Add(move);
            onMoveMade?.Invoke(move);
            boardUI.OnMoveMade(board, move, true);

            NotifyPlayerToMove();
        }

        public void NewGame(bool humanPlaysWhite)
        {
            boardUI.SetPerspective(humanPlaysWhite);
            NewGame((humanPlaysWhite) ? PlayerType.Human : PlayerType.AI, (humanPlaysWhite) ? PlayerType.AI : PlayerType.Human);
        }

        public IEnumerator NewComputerVersusComputerGame(bool record)
        {
            float[] colors = new float[] { Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f) };
            colors[Random.Range(0, 2)] = 1f;
            boardTheme.lightSquares.normal = new Color(
                colors[0],
                colors[1],
                colors[2]
            );

            gameMoves.Clear();
            board.LoadStartPosition();
            boardUI.SetPerspective(true);
            boardUI.ResetSquareColours();
            boardUI.UpdatePosition(board);
                        
            yield return new WaitForSeconds(1f);

            if (record)
            {
                StartRecorder();
            }

            yield return new WaitForSeconds(0.5f);

            NewGame(PlayerType.AI, PlayerType.AI);
            count++;            
        }

        void NewGame(PlayerType whitePlayerType, PlayerType blackPlayerType)
        {
            boardUI.changeDefaultSound();
            gameMoves.Clear();
            if (loadCustomPosition)
            {
                board.LoadPosition(customPosition);
                searchBoard.LoadPosition(customPosition);
            }
            else
            {
                board.LoadStartPosition();
                searchBoard.LoadStartPosition();
            }
            onPositionLoaded?.Invoke();
            boardUI.UpdatePosition(board);
            boardUI.ResetSquareColours();

            CreatePlayer(ref whitePlayer, whitePlayerType);
            CreatePlayer(ref blackPlayer, blackPlayerType);

            gameResult = Result.Playing;
            PrintGameResult(gameResult);

            NotifyPlayerToMove();
        }

        void LogAIDiagnostics()
        {
            string text = "";
            var d = aiSettings.diagnostics;
            //text += "AI Diagnostics";
            text += $"<color=#{ColorUtility.ToHtmlStringRGB(colors[3])}>Version 1.0\n";
            text += $"<color=#{ColorUtility.ToHtmlStringRGB(colors[0])}>Depth Searched: {d.lastCompletedDepth}";
            //text += $"\nPositions evaluated: {d.numPositionsEvaluated}";

            string evalString = "";
            if (d.isBook)
            {
                evalString = "Book";
            }
            else
            {
                float displayEval = d.eval / 100f;
                if (playerToMove is AIPlayer && !board.WhiteToMove)
                {
                    displayEval = -displayEval;
                }
                evalString = ($"{displayEval:00.00}").Replace(",", ".");
                if (Search.IsMateScore(d.eval))
                {
                    evalString = $"mate in {Search.NumPlyToMateFromScore(d.eval)} ply";
                }
            }
            text += $"\n<color=#{ColorUtility.ToHtmlStringRGB(colors[1])}>Eval: {evalString}";
            text += $"\n<color=#{ColorUtility.ToHtmlStringRGB(colors[2])}>Move: {d.moveVal}";

            aiDiagnosticsUI.text = text;
        }

        public void ExportGame(Result result)
        {
            IDictionary<object, object> dictionary = new Dictionary<object, object>();
            dictionary.Add("id", count);
            dictionary.Add("name", "Fantom Chess " + (count));
            dictionary.Add("description", "A random chess game");
            dictionary.Add("image", "{img}");

            string pgn = PGNCreator.CreatePGN(gameMoves.ToArray());
            List<Attributes> atts = new List<Attributes>();

            //rounds
            atts.Add(new Attributes("Moves", gameMoves.ToArray().Length.ToString()));
            
            //color
            atts.Add(new Attributes("Color", boardTheme.lightSquares.normal.ToString("F5")));
            
            //sound
            atts.Add(new Attributes("Sound", "Sound " + boardUI.selectedSound));
            
            //name 
            atts.Add(new Attributes("Championship Match", gameName));

            string winStr = "Draw";

            if (result == Result.Playing)
            {
                winStr = "";
            }
            else if (result == Result.WhiteIsMated || result == Result.BlackIsMated)
            {
                winStr = "Checkmate! " + (Result.WhiteIsMated == result ? "Black Win!" : "White Win!");
            }
            else if (result == Result.FiftyMoveRule)
            {
                winStr = "Draw - 50 move rule";
            }
            else if (result == Result.Repetition)
            {
                winStr = "Draw - 3 fold repetition";
            }
            else if (result == Result.Stalemate)
            {
                winStr = "Draw - Stalemate";
            }
            else if (result == Result.InsufficientMaterial)
            {
                winStr = "Draw - Insufficient material";
            }

            //winner
            atts.Add(new Attributes("Result", winStr));

            //pgn string
            atts.Add(new Attributes("PGN", pgn));
         
            List<IDictionary<object, object>> attributes = new List<IDictionary<object, object>>();

            foreach (var at in atts)
            {
                IDictionary<object, object> item = new Dictionary<object, object>();
                item.Add("trait_type", at.trait_type);
                item.Add("value", at.value);

                attributes.Add(item);
            }

            dictionary.Add("attributes", attributes);
            string json = Json.Serialize(dictionary);

            File.WriteAllText(@"Recordings\" + (count) + ".json", json);
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        void NotifyPlayerToMove()
        {
            gameResult = GetGameState();
            PrintGameResult(gameResult);

            if (gameResult == Result.Playing)
            {
                playerToMove = (board.WhiteToMove) ? whitePlayer : blackPlayer;
                playerToMove.NotifyTurnToMove();
            }
            else
            {
                StartCoroutine(awaitStopRecord(gameResult));
            }
        }

        

        void PrintGameResult(Result result)
        {
            float subtitleSize = resultUI.fontSize * 0.75f;
            string subtitleSettings = $"<color=#787878> <size={subtitleSize}>";

            if (result == Result.Playing)
            {
                resultUI.text = "";
            }
            else if (result == Result.WhiteIsMated || result == Result.BlackIsMated)
            {
                resultUI.text = "Checkmate!";
            }
            else if (result == Result.FiftyMoveRule)
            {
                resultUI.text = "Draw";
                resultUI.text += subtitleSettings + "\n(50 move rule)";
            }
            else if (result == Result.Repetition)
            {
                resultUI.text = "Draw";
                resultUI.text += subtitleSettings + "\n(3-fold repetition)";
            }
            else if (result == Result.Stalemate)
            {
                resultUI.text = "Draw";
                resultUI.text += subtitleSettings + "\n(Stalemate)";
            }
            else if (result == Result.InsufficientMaterial)
            {
                resultUI.text = "Draw";
                resultUI.text += subtitleSettings + "\n(Insufficient material)";
            }
        }

        Result GetGameState()
        {
            MoveGenerator moveGenerator = new MoveGenerator();
            var moves = moveGenerator.GenerateMoves(board);

            // Look for mate/stalemate
            if (moves.Count == 0)
            {
                if (moveGenerator.InCheck())
                {
                    return (board.WhiteToMove) ? Result.WhiteIsMated : Result.BlackIsMated;
                }
                return Result.Stalemate;
            }

            // Fifty move rule
            if (board.fiftyMoveCounter >= 100)
            {
                return Result.FiftyMoveRule;
            }

            // Threefold repetition
            int repCount = board.RepetitionPositionHistory.Count((x => x == board.ZobristKey));
            if (repCount == 3)
            {
                return Result.Repetition;
            }

            // Look for insufficient material (not all cases implemented yet)
            int numPawns = board.pawns[Board.WhiteIndex].Count + board.pawns[Board.BlackIndex].Count;
            int numRooks = board.rooks[Board.WhiteIndex].Count + board.rooks[Board.BlackIndex].Count;
            int numQueens = board.queens[Board.WhiteIndex].Count + board.queens[Board.BlackIndex].Count;
            int numKnights = board.knights[Board.WhiteIndex].Count + board.knights[Board.BlackIndex].Count;
            int numBishops = board.bishops[Board.WhiteIndex].Count + board.bishops[Board.BlackIndex].Count;

            if (numPawns + numRooks + numQueens == 0)
            {
                if (numKnights == 1 || numBishops == 1)
                {
                    return Result.InsufficientMaterial;
                }
            }

            return Result.Playing;
        }

        void CreatePlayer(ref Player player, PlayerType playerType)
        {
            if (player != null)
            {
                player.onMoveChosen -= OnMoveChosen;
            }

            if (playerType == PlayerType.Human)
            {
                player = new HumanPlayer(board);
            }
            else
            {
                player = new AIPlayer(searchBoard, aiSettings);
            }
            player.onMoveChosen += OnMoveChosen;
        }
    }


    [XmlRoot("GameCollection")]
    public class GameCollection
    {
        [XmlElement("Game")]
        public Game[] game;
    }

    public class Game
    {
        [XmlElement("winner")]
        public string winner;

        [XmlElement("pgn")]
        public string pgn;

        [XmlElement("name")]
        public string name;
    }
}

