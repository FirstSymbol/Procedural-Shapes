#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ProceduralShapes.Runtime;

namespace ProceduralShapes.Editor
{
    /// <summary>
    /// Предоставляет пункты меню в редакторе Unity для создания процедурных фигур.
    /// </summary>
    public static class ProceduralShapeMenus
    {
        private const string MENU_PATH = "GameObject/UI/Procedural Shapes/";

        /// <summary>
        /// Создает новый объект процедурной фигуры в UI.
        /// </summary>
        /// <param name="menuCommand">Контекст команды меню.</param>
        [MenuItem(MENU_PATH + "Shape", false, 10)]
        public static void CreateProceduralShape(MenuCommand menuCommand)
        {
            GameObject go = CreateUIObject("Procedural Shape", menuCommand.context as GameObject);
            go.AddComponent<ProceduralShape>();
            
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);

            FinalizeCreation(go);
        }

        /// <summary>
        /// Создает новый объект процедурной маски в UI.
        /// </summary>
        /// <param name="menuCommand">Контекст команды меню.</param>
        [MenuItem(MENU_PATH + "Shape Mask", false, 11)]
        public static void CreateProceduralShapeMask(MenuCommand menuCommand)
        {
            GameObject go = CreateUIObject("Procedural Shape Mask", menuCommand.context as GameObject);
            go.AddComponent<ProceduralShape>();
            go.AddComponent<ProceduralShapeMask>();
            
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);

            FinalizeCreation(go);
        }

        /// <summary>
        /// Создает базовый UI объект с RectTransform и CanvasRenderer.
        /// </summary>
        /// <param name="name">Имя создаваемого объекта.</param>
        /// <param name="parent">Родительский объект.</param>
        /// <returns>Созданный GameObject.</returns>
        private static GameObject CreateUIObject(string name, GameObject parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                parent = GetOrCreateCanvasGameObject();
            }

            GameObjectUtility.SetParentAndAlign(go, parent);
            go.layer = LayerMask.NameToLayer("UI");
            
            return go;
        }

        /// <summary>
        /// Завершает создание объекта: регистрирует Undo и выбирает его в иерархии.
        /// </summary>
        /// <param name="go">Созданный объект.</param>
        private static void FinalizeCreation(GameObject go)
        {
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            Selection.activeObject = go;
        }

        /// <summary>
        /// Находит существующий Canvas или создает новый, если он отсутствует.
        /// </summary>
        /// <returns>GameObject холста.</returns>
        private static GameObject GetOrCreateCanvasGameObject()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas != null && canvas.gameObject.activeInHierarchy)
                return canvas.gameObject;

            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvasGo;
        }
    }
}
#endif
