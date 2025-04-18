//----------------------------------------------
//            Hbx: UI
// Copyright © 2017-2018 Hogbox Studios
// WebGLRetinaToolsLite.cs v3.2
// Developed against WebGL build from Unity 2017.3.1f1
// Tested against Unity 5.6.0f3, 2017.3.1f1, 2018.1.6f1, 2018.2.0f2
//----------------------------------------------

//#define BROTLISTREAM_AVALIABLE

using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Hbx.WebGL
{

	public static class WebGLRetinaToolsLite
	{
		const string VERSION_STR = "3.3"; 

		static WebGLRetinaToolsLite()
		{
		}
		const string ProgressTitle = "Applying Retina Fix";
		const string JsExt = ".js";
		const string JsgzExt = ".jsgz";
		const string JsbrExt = ".jsbr";
		const string UnitywebExt = ".unityweb";
		#if UNITY_5_6_OR_NEWER
		const string DevFolder = "Build";
		static readonly string[] SourceFileTypes = {JsExt, UnitywebExt};
		static readonly string[] ExcludeFileNames = {"asm.memory", ".asm.code", ".data", "wasm.code"};
		#else
		const string DevFolder = "Development";
		static readonly string[] SourceFileTypes = {JsExt, JsgzExt, JsbrExt};
		static readonly string[] ExcludeFileNames = {"UnityLoader"};
		#endif

        public static List<string> _debugMessages = new List<string>();
		
		enum CompressionType
		{
			None,
			GZip,
			Brotli
		};

		[MenuItem("Hbx/WebGL/Fix Last Build (Lite)", false, 0)]
		public static void RetinaFixLastBuild()
		{
			if(EditorUserBuildSettings.development)
			{
				RetinaFixCodeFolder(DevFolder);
			}
			else
			{
				// only supports dev builds
                UnityEngine.Debug.LogError("WebGLRetinaToolsLite: Only development builds are supported in lite version.");
			}
		}

		//
		// Opens the jsgz and/or the js file in the current webgl build folder 
		// and inserts devicePixelRatio accordingly to add support for retina/hdpi 
		//
		//[MenuItem("Hbx/WebGL Tools/Retina Fix Release Build", false, 11)]
		public static void RetinaFixCodeFolder(string codeFolder)
		{
			UnityEngine.Debug.Log("WebGLRetinaToolsLite: Fix build started.");
			
			// get path of the last webgl build or use override path
			string webglBuildPath = EditorUserBuildSettings.GetBuildLocation(BuildTarget.WebGL);
			string codeFolderPath = Path.Combine(webglBuildPath, codeFolder);

			if(string.IsNullOrEmpty(codeFolderPath))
			{
				UnityEngine.Debug.LogError("WebGLRetinaToolsLite: WebGL build path is empty, have you created a WebGL build yet?");
				return;
			}
	
			// check there is a release folder
			if(!Directory.Exists(codeFolderPath))
			{
				UnityEngine.Debug.LogError("WebGLRetinaToolsLite: Couldn't find folder for WebGL build at path:\n" + codeFolderPath);
				return;
			}

			// find source files in release folder and fix
			string[] sourceFiles = FindSourceFilesInBuildFolder(codeFolderPath);
			foreach(string sourceFile in sourceFiles)
				FixSourceFile(sourceFile);
	
			UnityEngine.Debug.Log("WebGLRetinaToolsLite: Complete fixed " + sourceFiles.Length + " source files.");

            // Print report
            if (_debugMessages.Count > 0)
            {
                string report = "Following fixes applied...\n";
                foreach (string msg in _debugMessages)
                {
                    report += "    " + msg + "\n";
                }
                Debug.Log(report);
            }

			EditorUtility.ClearProgressBar();
		}

		//
		// Fix a source file based on it's extension type
		//
		static void FixSourceFile(string aSourceFile)
		{
			UnityEngine.Debug.Log("WebGLRetinaToolsLite: Fixing " + aSourceFile);
			FixJSFile(aSourceFile);
		}
		
		//
		// Fix a standard .js file
		//
		static void FixJSFile(string jsPath)
		{
			string fileName = Path.GetFileName(jsPath);

			EditorUtility.DisplayProgressBar(ProgressTitle, "Opening " + fileName + "...", 0.0f);

			UnityEngine.Debug.Log("WebGLRetinaToolsLite: Fixing raw JS file " + jsPath);

			// load the uncompressed js code
			string sourcestr = File.ReadAllText(jsPath);
			StringBuilder source = new StringBuilder(sourcestr);
			sourcestr = "";	

			EditorUtility.DisplayProgressBar(ProgressTitle, "Fixing js source in " + fileName + "...", 0.5f);
	
			FixJSFileContents(fileName.Contains(".wasm."), ref source);
	
			EditorUtility.DisplayProgressBar(ProgressTitle, "Saving js " + fileName + "...", 1.0f);
	
			// save the file
			File.WriteAllText(jsPath, source.ToString());
		}
		
		//
		// Search folder path for all supported SourceFileTypes
		// excluding any with names containing ExcludeFileNames
		//
		static string[] FindSourceFilesInBuildFolder(string aBuildPath)
		{
			string[] files = Directory.GetFiles(aBuildPath);
			List<string> found = new List<string>();
			foreach(string file in files)
			{
				string ext = Path.GetExtension(file); 
				if(Array.IndexOf(SourceFileTypes, ext) == -1) continue;
				string name = Path.GetFileNameWithoutExtension(file);
				bool exclude = false;
				foreach(string exname in ExcludeFileNames)
				{
					if(name.Contains(exname)) { exclude = true; break; }
				}
				if(!exclude) found.Add(file);
			}
			return found.ToArray();
		}

        //
        // Perform the find and replace hack for a development source
        //
        static void FixJSFileContents(bool iswasm, ref StringBuilder source)
        {
            int slength = source.Length;

#if UNITY_2018_2_OR_NEWER
            iswasm = false;
#endif

            // fix fill mouse event
            string findFillMouseString = "", replaceFillMouseString = "";
            if (!iswasm)
            {
#if UNITY_2018_2_OR_NEWER
                findFillMouseString =
@" fillMouseEventData: (function(eventStruct, e, target) {
  HEAPF64[eventStruct >> 3] = JSEvents.tick();
  HEAP32[eventStruct + 8 >> 2] = e.screenX;
  HEAP32[eventStruct + 12 >> 2] = e.screenY;
  HEAP32[eventStruct + 16 >> 2] = e.clientX;
  HEAP32[eventStruct + 20 >> 2] = e.clientY;
  HEAP32[eventStruct + 24 >> 2] = e.ctrlKey;
  HEAP32[eventStruct + 28 >> 2] = e.shiftKey;
  HEAP32[eventStruct + 32 >> 2] = e.altKey;
  HEAP32[eventStruct + 36 >> 2] = e.metaKey;
  HEAP16[eventStruct + 40 >> 1] = e.button;
  HEAP16[eventStruct + 42 >> 1] = e.buttons;
  HEAP32[eventStruct + 44 >> 2] = e[""movementX""] || e[""mozMovementX""] || e[""webkitMovementX""] || e.screenX - JSEvents.previousScreenX;
  HEAP32[eventStruct + 48 >> 2] = e[""movementY""] || e[""mozMovementY""] || e[""webkitMovementY""] || e.screenY - JSEvents.previousScreenY;
  if (Module[""canvas""]) {
   var rect = Module[""canvas""].getBoundingClientRect();
   HEAP32[eventStruct + 60 >> 2] = e.clientX - rect.left;
   HEAP32[eventStruct + 64 >> 2] = e.clientY - rect.top;
  } else {
   HEAP32[eventStruct + 60 >> 2] = 0;
   HEAP32[eventStruct + 64 >> 2] = 0;
  }
  if (target) {
   var rect = JSEvents.getBoundingClientRectOrZeros(target);
   HEAP32[eventStruct + 52 >> 2] = e.clientX - rect.left;
   HEAP32[eventStruct + 56 >> 2] = e.clientY - rect.top;
  } else {
   HEAP32[eventStruct + 52 >> 2] = 0;
   HEAP32[eventStruct + 56 >> 2] = 0;
  }
  if (e.type !== ""wheel"" && e.type !== ""mousewheel"") {
   JSEvents.previousScreenX = e.screenX;
   JSEvents.previousScreenY = e.screenY;
  }
 }),";

                replaceFillMouseString =
    @" fillMouseEventData: (function(eventStruct, e, target) {
  var devicePixelRatio = window.hbxDpr;
  HEAPF64[eventStruct >> 3] = JSEvents.tick();
  HEAP32[eventStruct + 8 >> 2] = e.screenX*devicePixelRatio;
  HEAP32[eventStruct + 12 >> 2] = e.screenY*devicePixelRatio;
  HEAP32[eventStruct + 16 >> 2] = e.clientX*devicePixelRatio;
  HEAP32[eventStruct + 20 >> 2] = e.clientY*devicePixelRatio;
  HEAP32[eventStruct + 24 >> 2] = e.ctrlKey;
  HEAP32[eventStruct + 28 >> 2] = e.shiftKey;
  HEAP32[eventStruct + 32 >> 2] = e.altKey;
  HEAP32[eventStruct + 36 >> 2] = e.metaKey;
  HEAP16[eventStruct + 40 >> 1] = e.button;
  HEAP16[eventStruct + 42 >> 1] = e.buttons;
  HEAP32[eventStruct + 44 >> 2] = e[""movementX""] || e[""mozMovementX""] || e[""webkitMovementX""] || (e.screenX*devicePixelRatio) - JSEvents.previousScreenX;
  HEAP32[eventStruct + 48 >> 2] = e[""movementY""] || e[""mozMovementY""] || e[""webkitMovementY""] || (e.screenY*devicePixelRatio) - JSEvents.previousScreenY;
  if (Module[""canvas""]) {
   var rect = Module[""canvas""].getBoundingClientRect();
   HEAP32[eventStruct + 60 >> 2] = (e.clientX - rect.left) * devicePixelRatio;
   HEAP32[eventStruct + 64 >> 2] = (e.clientY - rect.top) * devicePixelRatio;
  } else {
   HEAP32[eventStruct + 60 >> 2] = 0;
   HEAP32[eventStruct + 64 >> 2] = 0;
  }
  if (target) {
   var rect = JSEvents.getBoundingClientRectOrZeros(target);
   HEAP32[eventStruct + 52 >> 2] = (e.clientX - rect.left) * devicePixelRatio;
   HEAP32[eventStruct + 56 >> 2] = (e.clientY - rect.top) * devicePixelRatio;
  } else {
   HEAP32[eventStruct + 52 >> 2] = 0;
   HEAP32[eventStruct + 56 >> 2] = 0;
  }
  if (e.type !== ""wheel"" && e.type !== ""mousewheel"") {
   JSEvents.previousScreenX = e.screenX*devicePixelRatio;
   JSEvents.previousScreenY = e.screenY*devicePixelRatio;
  }
 }),";

#else
                findFillMouseString =
    @" fillMouseEventData: (function(eventStruct, e, target) {
  HEAPF64[eventStruct >> 3] = JSEvents.tick();
  HEAP32[eventStruct + 8 >> 2] = e.screenX;
  HEAP32[eventStruct + 12 >> 2] = e.screenY;
  HEAP32[eventStruct + 16 >> 2] = e.clientX;
  HEAP32[eventStruct + 20 >> 2] = e.clientY;
  HEAP32[eventStruct + 24 >> 2] = e.ctrlKey;
  HEAP32[eventStruct + 28 >> 2] = e.shiftKey;
  HEAP32[eventStruct + 32 >> 2] = e.altKey;
  HEAP32[eventStruct + 36 >> 2] = e.metaKey;
  HEAP16[eventStruct + 40 >> 1] = e.button;
  HEAP16[eventStruct + 42 >> 1] = e.buttons;
  HEAP32[eventStruct + 44 >> 2] = e[""movementX""] || e[""mozMovementX""] || e[""webkitMovementX""] || e.screenX - JSEvents.previousScreenX;
  HEAP32[eventStruct + 48 >> 2] = e[""movementY""] || e[""mozMovementY""] || e[""webkitMovementY""] || e.screenY - JSEvents.previousScreenY;
  if (Module[""canvas""]) {
   var rect = Module[""canvas""].getBoundingClientRect();
   HEAP32[eventStruct + 60 >> 2] = e.clientX - rect.left;
   HEAP32[eventStruct + 64 >> 2] = e.clientY - rect.top;
  } else {
   HEAP32[eventStruct + 60 >> 2] = 0;
   HEAP32[eventStruct + 64 >> 2] = 0;
  }
  if (target) {
   var rect = JSEvents.getBoundingClientRectOrZeros(target);
   HEAP32[eventStruct + 52 >> 2] = e.clientX - rect.left;
   HEAP32[eventStruct + 56 >> 2] = e.clientY - rect.top;
  } else {
   HEAP32[eventStruct + 52 >> 2] = 0;
   HEAP32[eventStruct + 56 >> 2] = 0;
  }
  JSEvents.previousScreenX = e.screenX;
  JSEvents.previousScreenY = e.screenY;
 }),";

                replaceFillMouseString =
    @" fillMouseEventData: (function(eventStruct, e, target) {
  var devicePixelRatio = window.hbxDpr;
  HEAPF64[eventStruct >> 3] = JSEvents.tick();
  HEAP32[eventStruct + 8 >> 2] = e.screenX*devicePixelRatio;
  HEAP32[eventStruct + 12 >> 2] = e.screenY*devicePixelRatio;
  HEAP32[eventStruct + 16 >> 2] = e.clientX*devicePixelRatio;
  HEAP32[eventStruct + 20 >> 2] = e.clientY*devicePixelRatio;
  HEAP32[eventStruct + 24 >> 2] = e.ctrlKey;
  HEAP32[eventStruct + 28 >> 2] = e.shiftKey;
  HEAP32[eventStruct + 32 >> 2] = e.altKey;
  HEAP32[eventStruct + 36 >> 2] = e.metaKey;
  HEAP16[eventStruct + 40 >> 1] = e.button;
  HEAP16[eventStruct + 42 >> 1] = e.buttons;
  HEAP32[eventStruct + 44 >> 2] = e[""movementX""] || e[""mozMovementX""] || e[""webkitMovementX""] || (e.screenX*devicePixelRatio) - JSEvents.previousScreenX;
  HEAP32[eventStruct + 48 >> 2] = e[""movementY""] || e[""mozMovementY""] || e[""webkitMovementY""] || (e.screenY*devicePixelRatio) - JSEvents.previousScreenY;
  if (Module[""canvas""]) {
   var rect = Module[""canvas""].getBoundingClientRect();
   HEAP32[eventStruct + 60 >> 2] = (e.clientX - rect.left) * devicePixelRatio;
   HEAP32[eventStruct + 64 >> 2] = (e.clientY - rect.top) * devicePixelRatio;
  } else {
   HEAP32[eventStruct + 60 >> 2] = 0;
   HEAP32[eventStruct + 64 >> 2] = 0;
  }
  if (target) {
   var rect = JSEvents.getBoundingClientRectOrZeros(target);
   HEAP32[eventStruct + 52 >> 2] = (e.clientX - rect.left) * devicePixelRatio;
   HEAP32[eventStruct + 56 >> 2] = (e.clientY - rect.top) * devicePixelRatio;
  } else {
   HEAP32[eventStruct + 52 >> 2] = 0;
   HEAP32[eventStruct + 56 >> 2] = 0;
  }
  JSEvents.previousScreenX = e.screenX*devicePixelRatio;
  JSEvents.previousScreenY = e.screenY*devicePixelRatio;
 }),";

#endif
            }
            else
            {

                findFillMouseString =
    @"fillMouseEventData:function (eventStruct, e, target) {
        HEAPF64[((eventStruct)>>3)]=JSEvents.tick();
        HEAP32[(((eventStruct)+(8))>>2)]=e.screenX;
        HEAP32[(((eventStruct)+(12))>>2)]=e.screenY;
        HEAP32[(((eventStruct)+(16))>>2)]=e.clientX;
        HEAP32[(((eventStruct)+(20))>>2)]=e.clientY;
        HEAP32[(((eventStruct)+(24))>>2)]=e.ctrlKey;
        HEAP32[(((eventStruct)+(28))>>2)]=e.shiftKey;
        HEAP32[(((eventStruct)+(32))>>2)]=e.altKey;
        HEAP32[(((eventStruct)+(36))>>2)]=e.metaKey;
        HEAP16[(((eventStruct)+(40))>>1)]=e.button;
        HEAP16[(((eventStruct)+(42))>>1)]=e.buttons;
        HEAP32[(((eventStruct)+(44))>>2)]=e[""movementX""] || e[""mozMovementX""] || e[""webkitMovementX""] || (e.screenX-JSEvents.previousScreenX);
        HEAP32[(((eventStruct)+(48))>>2)]=e[""movementY""] || e[""mozMovementY""] || e[""webkitMovementY""] || (e.screenY-JSEvents.previousScreenY);
  
        if (Module['canvas']) {
          var rect = Module['canvas'].getBoundingClientRect();
          HEAP32[(((eventStruct)+(60))>>2)]=e.clientX - rect.left;
          HEAP32[(((eventStruct)+(64))>>2)]=e.clientY - rect.top;
        } else { // Canvas is not initialized, return 0.
          HEAP32[(((eventStruct)+(60))>>2)]=0;
          HEAP32[(((eventStruct)+(64))>>2)]=0;
        }
        if (target) {
          var rect = JSEvents.getBoundingClientRectOrZeros(target);
          HEAP32[(((eventStruct)+(52))>>2)]=e.clientX - rect.left;
          HEAP32[(((eventStruct)+(56))>>2)]=e.clientY - rect.top;        
        } else { // No specific target passed, return 0.
          HEAP32[(((eventStruct)+(52))>>2)]=0;
          HEAP32[(((eventStruct)+(56))>>2)]=0;
        }
        JSEvents.previousScreenX = e.screenX;
        JSEvents.previousScreenY = e.screenY;
      },";

                replaceFillMouseString =
    @"fillMouseEventData:function (eventStruct, e, target) {
        var devicePixelRatio = window.hbxDpr;
        HEAPF64[((eventStruct)>>3)]=JSEvents.tick();
        HEAP32[(((eventStruct)+(8))>>2)]=e.screenX*devicePixelRatio;
        HEAP32[(((eventStruct)+(12))>>2)]=e.screenY*devicePixelRatio;
        HEAP32[(((eventStruct)+(16))>>2)]=e.clientX*devicePixelRatio;
        HEAP32[(((eventStruct)+(20))>>2)]=e.clientY*devicePixelRatio;
        HEAP32[(((eventStruct)+(24))>>2)]=e.ctrlKey;
        HEAP32[(((eventStruct)+(28))>>2)]=e.shiftKey;
        HEAP32[(((eventStruct)+(32))>>2)]=e.altKey;
        HEAP32[(((eventStruct)+(36))>>2)]=e.metaKey;
        HEAP16[(((eventStruct)+(40))>>1)]=e.button;
        HEAP16[(((eventStruct)+(42))>>1)]=e.buttons;
        HEAP32[(((eventStruct)+(44))>>2)]=e[""movementX""] || e[""mozMovementX""] || e[""webkitMovementX""] || ((e.screenX*devicePixelRatio)-JSEvents.previousScreenX);
        HEAP32[(((eventStruct)+(48))>>2)]=e[""movementY""] || e[""mozMovementY""] || e[""webkitMovementY""] || ((e.screenY*devicePixelRatio)-JSEvents.previousScreenY);
  
        if (Module['canvas']) {
          var rect = Module['canvas'].getBoundingClientRect();
          HEAP32[(((eventStruct)+(60))>>2)]=(e.clientX - rect.left)*devicePixelRatio;
          HEAP32[(((eventStruct)+(64))>>2)]=(e.clientY - rect.top)*devicePixelRatio;
        } else { // Canvas is not initialized, return 0.
          HEAP32[(((eventStruct)+(60))>>2)]=0;
          HEAP32[(((eventStruct)+(64))>>2)]=0;
        }
        if (target) {
          var rect = JSEvents.getBoundingClientRectOrZeros(target);
          HEAP32[(((eventStruct)+(52))>>2)]=(e.clientX - rect.left)*devicePixelRatio;
          HEAP32[(((eventStruct)+(56))>>2)]=(e.clientY - rect.top)*devicePixelRatio;        
        } else { // No specific target passed, return 0.
          HEAP32[(((eventStruct)+(52))>>2)]=0;
          HEAP32[(((eventStruct)+(56))>>2)]=0;
        }
        JSEvents.previousScreenX = e.screenX*devicePixelRatio;
        JSEvents.previousScreenY = e.screenY*devicePixelRatio;
      },";

            }

            source.Replace(findFillMouseString, replaceFillMouseString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 01");

#if UNITY_5_6_OR_NEWER
            // fix SystemInfo screen width height 
            string findSystemInfoString =
@"    return {
      width: screen.width ? screen.width : 0,
      height: screen.height ? screen.height : 0,
      browser: browser,";

            string replaceSystemInfoString =
@"    return {
      devicePixelRatio: window.hbxDpr,
      width: screen.width ? screen.width * this.devicePixelRatio : 0,
      height: screen.height ? screen.height * this.devicePixelRatio : 0,
      browser: browser,";
#else
            // fix SystemInfo screen width height 
            string findSystemInfoString =
@"var systemInfo = {
 get: (function() {
  if (systemInfo.hasOwnProperty(""hasWebGL"")) return this;
  var unknown = ""-"";
  this.width = screen.width ? screen.width : 0;
  this.height = screen.height ? screen.height : 0;";

            string replaceSystemInfoString =
@"var systemInfo = {
 get: (function() {
  if (systemInfo.hasOwnProperty(""hasWebGL"")) return this;
  var unknown = ""-"";
  var devicePixelRatio = window.hbxDpr;
  this.width = screen.width ? screen.width*devicePixelRatio : 0;
  this.height = screen.height ? screen.height*devicePixelRatio : 0;";
#endif

            slength = source.Length;
            source.Replace(findSystemInfoString, replaceSystemInfoString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 02");


            // fix _JS_SystemInfo_GetCurrentCanvasHeight

            string findGetCurrentCanvasHeightString = !iswasm ?
@"function _JS_SystemInfo_GetCurrentCanvasHeight() {
 return Module[""canvas""].clientHeight;
}" :
@"function _JS_SystemInfo_GetCurrentCanvasHeight() 
    {
        return Module['canvas'].clientHeight;
    }";

            string replaceGetCurrentCanvasHeightString =
@"function _JS_SystemInfo_GetCurrentCanvasHeight() {
 return Module[""canvas""].clientHeight*window.hbxDpr;
}";

            slength = source.Length;
            source.Replace(findGetCurrentCanvasHeightString, replaceGetCurrentCanvasHeightString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 03");


            // fix get _JS_SystemInfo_GetCurrentCanvasWidth

            string findGetCurrentCanvasWidthString = !iswasm ?
@"function _JS_SystemInfo_GetCurrentCanvasWidth() {
 return Module[""canvas""].clientWidth;
}" :
@"function _JS_SystemInfo_GetCurrentCanvasWidth() 
    {
        return Module['canvas'].clientWidth;
    }";

            string replaceGetCurrentCanvasWidthString =
@"function _JS_SystemInfo_GetCurrentCanvasWidth() {
 return Module[""canvas""].clientWidth*window.hbxDpr;
}";

            slength = source.Length;
            source.Replace(findGetCurrentCanvasWidthString, replaceGetCurrentCanvasWidthString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 04");


            // fix updateCanvasDimensions

            string findUpdateCanvasString = !iswasm ?
@"if ((document[""fullscreenElement""] || document[""mozFullScreenElement""] || document[""msFullscreenElement""] || document[""webkitFullscreenElement""] || document[""webkitCurrentFullScreenElement""]) === canvas.parentNode && typeof screen != ""undefined"") {
   var factor = Math.min(screen.width / w, screen.height / h);
   w = Math.round(w * factor);
   h = Math.round(h * factor);
  }
  if (Browser.resizeCanvas) {
   if (canvas.width != w) canvas.width = w;
   if (canvas.height != h) canvas.height = h;
   if (typeof canvas.style != ""undefined"") {
    canvas.style.removeProperty(""width"");
    canvas.style.removeProperty(""height"");
   }
  } else {
   if (canvas.width != wNative) canvas.width = wNative;
   if (canvas.height != hNative) canvas.height = hNative;
   if (typeof canvas.style != ""undefined"") {
    if (w != wNative || h != hNative) {
     canvas.style.setProperty(""width"", w + ""px"", ""important"");
     canvas.style.setProperty(""height"", h + ""px"", ""important"");
    } else {
     canvas.style.removeProperty(""width"");
     canvas.style.removeProperty(""height"");
    }
   }
  }" :
@"if (((document['fullscreenElement'] || document['mozFullScreenElement'] ||
             document['msFullscreenElement'] || document['webkitFullscreenElement'] ||
             document['webkitCurrentFullScreenElement']) === canvas.parentNode) && (typeof screen != 'undefined')) {
           var factor = Math.min(screen.width / w, screen.height / h);
           w = Math.round(w * factor);
           h = Math.round(h * factor);
        }
        if (Browser.resizeCanvas) {
          if (canvas.width  != w) canvas.width  = w;
          if (canvas.height != h) canvas.height = h;
          if (typeof canvas.style != 'undefined') {
            canvas.style.removeProperty( ""width"");
            canvas.style.removeProperty(""height"");
          }
        } else {
          if (canvas.width  != wNative) canvas.width  = wNative;
          if (canvas.height != hNative) canvas.height = hNative;
          if (typeof canvas.style != 'undefined') {
            if (w != wNative || h != hNative) {
              canvas.style.setProperty( ""width"", w + ""px"", ""important"");
              canvas.style.setProperty(""height"", h + ""px"", ""important"");
            } else {
              canvas.style.removeProperty( ""width"");
              canvas.style.removeProperty(""height"");
            }
          }
        }";

            string replaceUpdateCanvasString =
@"var dpr = window.hbxDpr;
  if ((document[""fullscreenElement""] || document[""mozFullScreenElement""] || document[""msFullscreenElement""] || document[""webkitFullscreenElement""] || document[""webkitCurrentFullScreenElement""]) === canvas.parentNode && typeof screen != ""undefined"") {
   var factor = Math.min((screen.width*dpr) / w, (screen.height*dpr) / h);
   w = Math.round(w * factor);
   h = Math.round(h * factor);
  }
  if (Browser.resizeCanvas) {
   if (canvas.width != w) canvas.width = w;
   if (canvas.height != h) canvas.height = h;
   if (typeof canvas.style != ""undefined"") {
    canvas.style.removeProperty(""width"");
    canvas.style.removeProperty(""height"");
   }
  } else {
   if (canvas.width != wNative) canvas.width = wNative;
   if (canvas.height != hNative) canvas.height = hNative;
   if (typeof canvas.style != ""undefined"") {
     if(!canvas.style.getPropertyValue(""width"").includes(""%""))canvas.style.setProperty(""width"", (w/dpr) + ""px"", ""important"");
     if(!canvas.style.getPropertyValue(""height"").includes(""%""))canvas.style.setProperty(""height"", (h/dpr) + ""px"", ""important"");
   }
  }";

            slength = source.Length;
            source.Replace(findUpdateCanvasString, replaceUpdateCanvasString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 05");


            string findFullscreenEventString = !iswasm ?
@"  HEAP32[eventStruct + 264 >> 2] = reportedElement ? reportedElement.clientWidth : 0;
  HEAP32[eventStruct + 268 >> 2] = reportedElement ? reportedElement.clientHeight : 0;
  HEAP32[eventStruct + 272 >> 2] = screen.width;
  HEAP32[eventStruct + 276 >> 2] = screen.height;" :
@"        HEAP32[(((eventStruct)+(264))>>2)]=reportedElement ? reportedElement.clientWidth : 0;
        HEAP32[(((eventStruct)+(268))>>2)]=reportedElement ? reportedElement.clientHeight : 0;
        HEAP32[(((eventStruct)+(272))>>2)]=screen.width;
        HEAP32[(((eventStruct)+(276))>>2)]=screen.height;";

            string replaceFullscreenEventString =
@"  HEAP32[eventStruct + 264 >> 2] = reportedElement ? reportedElement.clientWidth : 0;
  HEAP32[eventStruct + 268 >> 2] = reportedElement ? reportedElement.clientHeight : 0;
  HEAP32[eventStruct + 272 >> 2] = screen.width * window.hbxDpr;
  HEAP32[eventStruct + 276 >> 2] = screen.height * window.hbxDpr;";

            slength = source.Length;
            source.Replace(findFullscreenEventString, replaceFullscreenEventString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 06");


#if UNITY_5_6_OR_NEWER
            //
            // touches
            string findTouchesString =
@"for (var i in touches) {
    var t = touches[i];
    HEAP32[ptr >> 2] = t.identifier;
    HEAP32[ptr + 4 >> 2] = t.screenX;
    HEAP32[ptr + 8 >> 2] = t.screenY;
    HEAP32[ptr + 12 >> 2] = t.clientX;
    HEAP32[ptr + 16 >> 2] = t.clientY;
    HEAP32[ptr + 20 >> 2] = t.pageX;
    HEAP32[ptr + 24 >> 2] = t.pageY;
    HEAP32[ptr + 28 >> 2] = t.changed;
    HEAP32[ptr + 32 >> 2] = t.onTarget;
    if (canvasRect) {
     HEAP32[ptr + 44 >> 2] = t.clientX - canvasRect.left;
     HEAP32[ptr + 48 >> 2] = t.clientY - canvasRect.top;
    } else {
     HEAP32[ptr + 44 >> 2] = 0;
     HEAP32[ptr + 48 >> 2] = 0;
    }
    HEAP32[ptr + 36 >> 2] = t.clientX - targetRect.left;
    HEAP32[ptr + 40 >> 2] = t.clientY - targetRect.top;
    ptr += 52;
    if (++numTouches >= 32) {
     break;
    }
   }";

            string replaceTouchesString =
@" var devicePixelRatio = window.hbxDpr;
   for (var i in touches) {
    var t = touches[i];
    HEAP32[ptr >> 2] = t.identifier;
    HEAP32[ptr + 4 >> 2] = t.screenX*devicePixelRatio;
    HEAP32[ptr + 8 >> 2] = t.screenY*devicePixelRatio;
    HEAP32[ptr + 12 >> 2] = t.clientX*devicePixelRatio;
    HEAP32[ptr + 16 >> 2] = t.clientY*devicePixelRatio;
    HEAP32[ptr + 20 >> 2] = t.pageX*devicePixelRatio;
    HEAP32[ptr + 24 >> 2] = t.pageY*devicePixelRatio;
    HEAP32[ptr + 28 >> 2] = t.changed;
    HEAP32[ptr + 32 >> 2] = t.onTarget;
    if (canvasRect) {
     HEAP32[ptr + 44 >> 2] = (t.clientX - canvasRect.left) * devicePixelRatio;
     HEAP32[ptr + 48 >> 2] = (t.clientY - canvasRect.top) * devicePixelRatio;
    } else {
     HEAP32[ptr + 44 >> 2] = 0;
     HEAP32[ptr + 48 >> 2] = 0;
    }
    HEAP32[ptr + 36 >> 2] = (t.clientX - targetRect.left) * devicePixelRatio;
    HEAP32[ptr + 40 >> 2] = (t.clientY - targetRect.top) * devicePixelRatio;
    ptr += 52;
    if (++numTouches >= 32) {
     break;
    }
   }";
            slength = source.Length;
            source.Replace(findTouchesString, replaceTouchesString);
            if (slength != source.Length) _debugMessages.Add("Applied fix 07");

#endif

            // instert dpr initalise code
#if UNITY_2018_1_OR_NEWER

            // this only needs to apply to UnityLoader.js
#if UNITY_2019_1_OR_NEWER
            string findDPRInsertPoint =
@"compatibilityCheck: function (unityInstance, onsuccess, onerror) {";
#else
            string findDPRInsertPoint =
@"compatibilityCheck: function (gameInstance, onsuccess, onerror) {";
#endif


            string replaceDPRInsertPoint = findDPRInsertPoint +
@"
    window.devicePixelRatio = window.devicePixelRatio || 1;
    window.hbxDpr = window.devicePixelRatio;";

            slength = source.Length;
            source.Replace(findDPRInsertPoint, replaceDPRInsertPoint);
            if (slength != source.Length) _debugMessages.Add("Applied fix 08");

#else
            // this only needs to apply to UnityLoader.js
            string findDPRInsertPoint =
@"var UnityLoader = UnityLoader || {
  compatibilityCheck: function (gameInstance, onsuccess, onerror) {";

            string replaceDPRInsertPoint =
@"var UnityLoader = UnityLoader || {
  compatibilityCheck: function (gameInstance, onsuccess, onerror) {
    var dprs = UnityLoader.SystemInfo.mobile ? " + MobileScale + " : " + DesktopScale + @";
    window.devicePixelRatio = window.devicePixelRatio || 1;
    window.hbxDpr = window.devicePixelRatio * dprs;";

            slength = source.Length;
            source.Replace(findDPRInsertPoint, replaceDPRInsertPoint);
            if (slength != source.Length) _debugMessages.Add("Applied fix 08");
#endif
        }
    }

} // end Hbx WebGL namespace
