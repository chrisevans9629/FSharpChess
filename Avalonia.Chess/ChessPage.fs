namespace Avalonia.Chess
open Elmish
open Chess.Pieces
open Avalonia.FuncUI.Components
open Avalonia
open Avalonia.Media.Imaging

module ChessPage =
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout
    open Chess.ChessParser
    open Chess.ChessActions
    open Chess.ChessGrid
    open Chess.ChessAi
    open Chess.ChessIcons

    type State = { 
        game : Piece option list
        fromPos : ChessBoardId option
        Moves: (ChessBoardId * ChessBoardId) list
        Player: Color
        GameOver: EndGame
        Turn: Color
        Ai: Color
        HighlightedPiece: ChessBoardId option
        }
    let init = { 
        game = initialGame
        fromPos = None
        Moves=[]
        Player = White
        GameOver = Na
        Turn=White
        Ai=Black 
        HighlightedPiece=None }

    type Msg = 
        | From of ChessBoardId 
        | To of ChessBoardId
        | Reset 
        | RemovePawns 
        | Flip 
        | CopyMoves
        | EndGame
        | Undo
        | Highlighted of ChessBoardId option
        | MoveAi of (int * int) option
        

    let getMoveAlpha (f,t) =
        sprintf "%s-%s" (getAlpha f) (getAlpha t)

    let addMoves moves fromId toId =
        [fromId,toId] |> List.append moves

    let minimaxAsync state = 
        async { let move, _ = minimax2 state.game true state.Turn
                do! Async.Sleep 500
                return move }
    let update (msg: Msg) (state: State) =
        match msg with
        | MoveAi move ->
            match move with
            | Some (eFromId, eToId) -> 
                let eMoveAction = moveById state.game (ChessId eFromId) (ChessId eToId)
                { state with game = eMoveAction; GameOver = gameOver eMoveAction; Turn = swap state.Turn; Moves = addMoves state.Moves (ChessId eFromId) (ChessId eToId) }, Cmd.none
            | None -> state, Cmd.none
        | From v -> { state with fromPos = Some v }, Cmd.none
        | To v -> 
            let fromId = state.fromPos |> Option.get
            if isValidMoveById state.game fromId v then
                let moveAction = moveById state.game fromId v
                let enemy = swap state.Turn

                let nState = { state with 
                                fromPos = None; 
                                game = moveAction; 
                                Moves = addMoves state.Moves fromId v; 
                                GameOver = gameOver moveAction; 
                                Turn = enemy }

                nState, Cmd.OfAsync.perform minimaxAsync nState (fun r -> MoveAi r)
            else 
                { state with fromPos = None }, Cmd.none
        | Reset -> init, Cmd.none
        | RemovePawns -> { init with game = noPawnGame }, Cmd.none
        | Flip -> { state with Player = swap state.Player  }, Cmd.none
        | CopyMoves -> 
            Application.Current.Clipboard.SetTextAsync (state.Moves |> List.map getMoveAlpha |> String.concat ",") |> ignore
            state, Cmd.none
        | EndGame -> {init with game = endGame }, Cmd.none
        | Undo -> 
            if state.Moves.IsEmpty then state, Cmd.none
            else
                let fromId,toId = state.Moves.[state.Moves.Length-1]
                let moves = state.Moves |> List.filter (fun r -> r <> (fromId,toId))
                let action = moveById state.game toId fromId
                {state with Moves = moves; game = action}, Cmd.none 
        | Highlighted v -> {state with HighlightedPiece = v}, Cmd.none
            
    
    let button dispatch name action =
        Button.create [
            Button.onClick (fun _ -> dispatch action)
            Button.content (name |> string)
        ]

    let gameOverView (state: State) =
        StackPanel.create [
            Grid.row 1
            Grid.column 0
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.children [
                TextBlock.create [
                    let txt = match state.GameOver with
                                | Winner clr -> sprintf "Player %O Won!" clr
                                | Tie -> "Tie!"
                                | _ -> ""
                    TextBlock.text txt
                ]
            ]
        ]
    
    let darkColor = "#a6a693"
    let lightColor = "#769656"
    let selectColor = "lightblue"
    let highlightColor = "#baca44"

    let chessBoard (state: State) (dispatch) =
        let fromPos = state.fromPos
        let color id = lookup state.game id |> Option.map (fun r -> r.Color)
        
        Grid.create [
            Grid.isSharedSizeScope true
            Grid.row 1
            Grid.column 0
            Grid.rowDefinitions (System.String.Join(",",[for r in 0..7 -> "*"]))
            Grid.columnDefinitions (System.String.Join(",",[for r in 0..7 -> "*"]))
            Grid.children [
                for id in 0..63 -> 
                    let chessId = ChessId id
                    let idStr = sprintf "(%s %i)" (getAlpha chessId) id
                    let x,y = getXY chessId


                    let background = match fromPos, state.HighlightedPiece, state.Turn = White with 
                                                | Some pos, _, true when pos = chessId -> selectColor
                                                | Some pos, _, true when isValidMoveById state.game pos chessId -> highlightColor
                                                | _, Some pos, true when pos = chessId -> selectColor
                                                | _, Some pos, true when isValidMoveById state.game pos chessId -> highlightColor
                                                | _ -> 
                                                    if (id + (y % 2)) % 2 = 0 then
                                                        lightColor
                                                    else
                                                        darkColor
                    
                    let row = match state.Player with
                                | White -> 7-y
                                | Black -> y

                    Button.create [
                        Button.background background
                        Button.foreground "White"
                        ToolTip.tip (state.game.[id] |> Option.fold (fun _ a -> a.Name + " " + idStr) idStr)
                        
                        match state.game.[id] with
                        | Some p -> Button.content (Image.create [Image.source (new Bitmap(getIcon p))])
                        | None -> ()
                        Button.row row
                        Button.column x
                        Button.onPointerEnter (fun _ -> dispatch (Highlighted (Some chessId)))
                        Button.onPointerLeave (fun _ -> dispatch (Highlighted None))
                        match fromPos, state.game.[id] with 
                        | Some _, _ -> Button.onClick (fun _ -> dispatch (To chessId)) 
                        | None, Some item when item.Color = state.Turn -> Button.onClick (fun _ -> dispatch (From chessId))
                        | _,_ -> Button.onClick (fun _ -> ())
                    ]
            ]
        ]

    let toolPanel (state: State) (dispatch) =
        StackPanel.create [
            Grid.row 0
            Grid.column 0
            StackPanel.margin 10.
            StackPanel.spacing 10.
            StackPanel.orientation Orientation.Horizontal
            StackPanel.children [
                button dispatch "Reset" Reset
                button dispatch "No Pawn" RemovePawns
                button dispatch "Flip" Flip
                button dispatch "Copy Moves" CopyMoves
                button dispatch "End Game" EndGame
                button dispatch "Undo" Undo
                TextBlock.create [
                    TextBlock.text (sprintf "Player's %O Turn" state.Turn)
                ]
            ]
        ]

    let movesPanel (state: State) = 
        ListBox.create [
            Grid.row 1
            Grid.column 1
            ListBox.dataItems (state.Moves |> List.map getMoveAlpha)
            ListBox.itemTemplate (DataTemplateView<string>.create(fun item -> TextBlock.create[TextBlock.text item]))
        ]

    let view (state: State) (dispatch) =
        //let fromPiece = fromPos |> Option.map (fun r -> state.game.[r]) |> Option.flatten |> Option.map (fun r -> r.Color)
        Grid.create [
            Grid.rowDefinitions "Auto,*"
            Grid.columnDefinitions "*, 100"
            Grid.children [
                toolPanel state dispatch
                movesPanel state
                chessBoard state dispatch
                match state.GameOver with
                | Na -> ()
                | _ -> gameOverView state
            ]
        ]      