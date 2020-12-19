[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Ionide.VSCode.FSharp

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import.vscode
open Ionide.VSCode.Helpers
open Ionide.VSCode.FSharp
open global.Node.ChildProcess

type Api =
    { ProjectLoadedEvent : Event<DTO.Project>
      BuildProject : DTO.Project -> JS.Promise<string>
      BuildProjectFast : DTO.Project -> JS.Promise<string>
      GetProjectLauncher : OutputChannel -> DTO.Project -> (string -> JS.Promise<ChildProcess>) option
      DebugProject : DTO.Project -> string [] -> JS.Promise<unit> }

let activate (context : ExtensionContext) : JS.Promise<Api> =

    let solutionExplorer = "FSharp.enableTreeView" |> Configuration.get true
    let showExplorer = "FSharp.showExplorerOnStartup" |> Configuration.get true

    LanguageService.start context
    |> Promise.onSuccess (fun _ ->
        let progressOpts = createEmpty<ProgressOptions>
        progressOpts.location <- ProgressLocation.Window
        window.withProgress(progressOpts, (fun p ->
            let pm = createEmpty<ProgressMessage>
            pm.message <- "Loading projects"
            p.report pm

            Project.activate context
            |> Promise.onSuccess(fun _ -> QuickInfoProject.activate context )
            |> Promise.onSuccess (fun _ ->
                if showExplorer then
                    commands.executeCommand(VSCodeExtension.workbenchViewId ())
                    |> ignore)
            |> Promise.onSuccess (fun _ ->
                LanguageService.loadAnalyzers ()
                |> ignore
            )
        ))
        |> ignore
    )
    |> Promise.catch ignore // prevent unhandled rejected promises
    |> Promise.map (fun _ ->
        if solutionExplorer then SolutionExplorer.activate context
        Diagnostics.activate context
        LineLens.activate context
        QuickInfo.activate context
        Help.activate context
        MSBuild.activate context
        SignatureData.activate context
        Debugger.activate context
        Fsdn.activate context
        Fsi.activate context
        ScriptRunner.activate context
        LanguageConfiguration.activate context
        HtmlConverter.activate context
        InfoPanel.activate context
        CodeLensHelpers.activate context
        FakeTargetsOutline.activate context
        Gitignore.activate context
        HighlightingProvider.activate context
        FSharpLiterate.activate context
        PipelineHints.activate context

        let buildProject project = promise {
            let! exit = MSBuild.buildProjectPath "Build" project
            match exit.Code with
            | Some code -> return code.ToString()
            | None -> return ""
        }

        let buildProjectFast project = promise {
            let! exit = MSBuild.buildProjectPathFast project
            match exit.Code with
            | Some code -> return code.ToString()
            | None -> return ""
        }

        let event = EventEmitter<DTO.Project>()
        Project.projectLoaded.Invoke(fun n ->
            !!(setTimeout (fun _ -> event.fire n) 500.)
        ) |> ignore

        { ProjectLoadedEvent = event.event
          BuildProject = buildProject
          BuildProjectFast = buildProjectFast
          GetProjectLauncher = Project.getLauncher
          DebugProject = Debugger.debugProject }
    )

let deactivate(disposables : Disposable[]) =
    LanguageService.stop ()
