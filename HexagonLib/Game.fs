﻿module Hexagon.Game

open Domain
open Hexagon
open Hexagon.Shapes

let startGame raiseEvents (cancellationToken: System.Threading.CancellationToken) hexagonSize setTimeout ais =
    let nbPlayers = ais |> List.length
    let basicAis = 
        [nbPlayers + 1..6]
        |> List.map (fun i -> { Id = i; Name = sprintf "Basic AI %i" i }, Hexagon.BasicAi.play)
    let rand = fun () -> (new System.Random()).Next()
    let ais = 
        ais @ basicAis
        |> List.map (fun ai -> ai, rand())
        |> List.sortBy snd
        |> List.map fst
    
    let hexagon = 
        HexagonBoard.generate hexagonSize 
        |> convertShapeToCells
        |> Seq.toList

    let board = CellsStore.CellsStore(hexagon, HexagonCell.isNeighbours)
    let aiCellsIdsStore = AiCellIdsStore.Store(hexagon)
    
    let publishEvent evt =
        match evt with
        | Board (_, cellEvents, _) -> cellEvents |> Seq.iter board.apply
        | _ -> ()

        evt
        |> raiseEvents

    Started { 
        BoardSize = 
            { 
                Lines = hexagon |> Seq.map (fun c -> c.Id.LineNum) |> Seq.max 
                Columns = hexagon |> Seq.map (fun c -> c.Id.ColumnNum) |> Seq.max 
            } 
        Board = hexagon
        Ais = ais |> Seq.map fst }
    |> publishEvent
    
    ais
    |> Seq.map (fun ai -> (fst ai).Id)
    |> Seq.zip (hexagon |> Seq.filter (fun c -> c.IsStartingPosition) |> Seq.map (fun c -> c.Id))
    |> Seq.map (fun (cell, ai) -> 
        AiAdded { AiId = ai; CellId = cell; Resources = 1 }, // WTF??
        [Owned { AiId = ai; CellId = cell; Resources = 1 }], 
        [TerritoryChanged { AiId = ai; ResourcesIncrement = 1; CellsIncrement = 1}])
    |> Seq.map Board
    |> Seq.iter publishEvent
        
    let wrapAiPlay = Ais.AntiCorruptionLayer.wrap aiCellsIdsStore.convertToAiCellId aiCellsIdsStore.convertToCellId
    let round = Round.createRunRound board.getCellsWithNeighboursOf board.getCell board.isNeighboursOf board.getAllOwnCells

    let rec runRound (nb: int) ais = 
        round ais |> Seq.iter publishEvent
        System.Console.WriteLine ("Round " + nb.ToString())

        if cancellationToken.IsCancellationRequested |> not
        then 
            setTimeout (fun () -> runRound (nb + 1) ais)

    ais 
    |> List.map (fun (ai, play) -> (ai.Id, wrapAiPlay play)) 
    |> runRound 0