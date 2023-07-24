using System;
using System.IO;
using ChessChallenge.API;
using ChessChallenge.Chess;
using Board = ChessChallenge.API.Board;
using Move = ChessChallenge.API.Move;
using Timer = ChessChallenge.API.Timer;

namespace ChessChallenge.Application
{
    static class Program
    {

        private static String GetMoveNameUCI(Move move)
        {
            string startSquareName = BoardHelper.SquareNameFromIndex(move.StartSquare.Index);
            string endSquareName = BoardHelper.SquareNameFromIndex(move.TargetSquare.Index);
            string moveName = startSquareName + endSquareName;
            if (move.IsPromotion)
            {
                switch (move.PromotionPieceType)
                {
                    case PieceType.Rook:
                        moveName += "r";
                        break;
                    case PieceType.Knight:
                        moveName += "n";
                        break;
                    case PieceType.Bishop:
                        moveName += "b";
                        break;
                    case PieceType.Queen:
                        moveName += "q";
                        break;
                }
            }
            return moveName;
        }
        public static void Main()
        {
            String startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            MyBot myBot = new MyBot();

            Board board = Board.CreateBoardFromFEN(startFen);

            /*
            string path = Path.Combine(Directory.GetCurrentDirectory(), "src", "My Bot", "MyBot.cs");
            Console.WriteLine(path);
            using var stringReader = new StreamReader(path);
            string text_code = stringReader.ReadToEnd();
            Console.WriteLine(TokenCounter.CountTokens(text_code));
            */

            while (true)
            {
                String line = Console.ReadLine();
                String[] tokens = line.Split();

                if (tokens[0] == "quit")
                {
                    break;
                }

                if (tokens[0] == "ucinewgame")
                {
                    myBot.gamePly = 0;
                }

                if (tokens[0] == "uci")
                {
                    Console.WriteLine("id name Chess Challenge - Antares");
                    Console.WriteLine("id author Sebastian Lague, Antares");
                    Console.WriteLine("uciok");
                }

                if (tokens[0] == "isready")
                {
                    Console.WriteLine("readyok");
                }

                if (tokens[0] == "position")
                {
                    int nextIndex = 0;
                    if (tokens[1] == "startpos")
                    {
                        board = Board.CreateBoardFromFEN(startFen);
                        nextIndex = 2;
                    }

                    else if (tokens[1] == "fen")
                    {
                        String fen = "";
                        for (int i = 2; i < 8; i++)
                        {
                            fen += tokens[i];
                            fen += " ";
                        }

                        board = Board.CreateBoardFromFEN(fen);
                        nextIndex = 8;
                    }

                    else continue;

                    if (tokens.Length <= nextIndex || tokens[nextIndex] != "moves") continue;

                    for (int i = nextIndex + 1; i < tokens.Length; i++)
                    {
                        Move move = new Move(tokens[i], board);
                        board.MakeMove(move);
                    }
                }

                if (tokens[0] == "go")
                {
                    int allocatedTime = 0;
                    int wTime = 0;
                    int bTime = 0;
                    int wInc = 0;
                    int bInc = 0;

                    for (int i = 1; i < tokens.Length; i += 2)
                    {
                        String type = tokens[i];
                        int value = 0;

                        if (tokens.Length > i + 1) value = int.Parse(tokens[i + 1]);

                        if (type == "movetime") allocatedTime = (int)(value * 0.95);

                        if (type == "wtime") wTime = value;

                        if (type == "btime") bTime = value;

                        if (type == "winc") wInc = value;

                        if (type == "binc") bInc = value;
                    }

                    int selfTime = board.IsWhiteToMove ? wTime : bTime;
                    int selfInc = board.IsWhiteToMove ? wInc : bInc;

                    if (allocatedTime == 0)
                    {
                        allocatedTime = selfTime + selfInc;
                    }

                    Timer timer = new Timer(allocatedTime);
                    Move move = myBot.Think(board, timer);
                    Console.WriteLine("bestmove " + GetMoveNameUCI(move));
                }
            }
        }

      

    }


}