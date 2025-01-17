﻿using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class MapTab {
      private MapEditorViewModel ViewModel => (MapEditorViewModel)DataContext;

      public MapTab() {
         InitializeComponent();
         DataContextChanged += UpdateDataContext;
      }

      protected override void OnVisualParentChanged(DependencyObject oldParent) {
         base.OnVisualParentChanged(oldParent);
         Focus();
      }

      private void UpdateDataContext(object sender, DependencyPropertyChangedEventArgs e) {
         var oldContext = e.OldValue as MapEditorViewModel;
         if (oldContext != null) {
            oldContext.PropertyChanged -= HandleContextPropertyChanged;
            oldContext.AutoscrollBlocks -= AutoscrollBlocks;
            oldContext.AutoscrollTiles -= AutoscrollTiles;
         }
         var newContext = e.NewValue as MapEditorViewModel;
         if (newContext != null) {
            newContext.PropertyChanged += HandleContextPropertyChanged;
            newContext.AutoscrollBlocks += AutoscrollBlocks;
            newContext.AutoscrollTiles += AutoscrollTiles;
         }
      }

      private void HandleContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         // TODO any custom property logic here
      }

      private void AutoscrollBlocks(object sender, EventArgs e) {
         var scrollRange = BlockViewer.ExtentHeight - BlockViewer.ViewportHeight;
         var blockHeight = (ViewModel.Blocks.PixelHeight / 16.0) * 8 - 16;
         var scrollPercent = ((ViewModel.DrawBlockIndex - 8) / blockHeight).LimitToRange(0, 1);
         BlockViewer.ScrollToVerticalOffset(scrollRange * scrollPercent);
      }

      private void AutoscrollTiles(object sender, EventArgs e) {
         var scrollRange = TileViewer.ExtentHeight - TileViewer.ViewportHeight;
         var tileHeight = ViewModel.PrimaryMap.BlockEditor.TileRender.PixelHeight * 3 - 24;
         var scrollPercent = (ViewModel.PrimaryMap.BlockEditor.TileSelectionY / (double)tileHeight).LimitToRange(0, 1);
         TileViewer.ScrollToVerticalOffset(scrollRange * scrollPercent);
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         var partial = ActualWidth / 2 - (int)(ActualWidth / 2);
         var transform = (TranslateTransform)MapView.RenderTransform;
         transform.X = -partial;
      }

      #region Map Interaction

      private MouseButton withinMapInteraction = MouseButton.XButton1; // track which button is being used. Set to XButton1 when not in use.

      private void ButtonDown(object sender, MouseButtonEventArgs e) {
         if (e.ChangedButton == MouseButton.XButton1 && ViewModel.Back.CanExecute(null)) {
            ViewModel.Back.Execute();
            return;
         }
         if (e.ChangedButton == MouseButton.XButton2 && ViewModel.Forward.CanExecute(null)) {
            ViewModel.Forward.Execute();
            return;
         }
         if (withinMapInteraction != MouseButton.XButton1) return;
         Focus();
         e.Handled = true;
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         element.CaptureMouse();
         if (e.LeftButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Left;
            vm.PrimaryDown(p.X, p.Y, e.ClickCount);
         } else if (e.MiddleButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Middle;
            vm.DragDown(p.X, p.Y);
         } else if (e.RightButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Right;
            var result = vm.SelectDown(p.X, p.Y);
            if (result == SelectionInteractionResult.ShowWarpMenu) {
               withinMapInteraction = MouseButton.XButton1;
               ShowWarpCreation(element, e);
            }
         }
      }

      private void ButtonMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         if (vm == null) return;
         var p = GetCoordinates(element, e);
         e.Handled = true;
         if (withinMapInteraction == MouseButton.XButton1) {
            vm.Hover(p.X, p.Y);
            return;
         }
         if (withinMapInteraction == MouseButton.Left) {
            vm.PrimaryMove(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Middle) {
            vm.DragMove(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Right) {
            vm.SelectMove(p.X, p.Y);
         }
      }

      private void ButtonUp(object sender, MouseButtonEventArgs e) {
         e.Handled = true;
         var element = (FrameworkElement)sender;
         var previousInteraction = withinMapInteraction;
         withinMapInteraction = MouseButton.XButton1;
         element.ReleaseMouseCapture();
         if (previousInteraction == MouseButton.XButton1) return;
         if (e.ChangedButton != previousInteraction) return;
         var vm = ViewModel;
         if (vm == null) return;
         var p = GetCoordinates(element, e);
         if (previousInteraction == MouseButton.Left) {
            vm.PrimaryUp(p.X, p.Y);
         } else if (previousInteraction == MouseButton.Middle) {
            vm.DragUp(p.X, p.Y);
         } else if (previousInteraction == MouseButton.Right) {
            vm.SelectUp(p.X, p.Y);
         }
      }

      private void Wheel(object sender, MouseWheelEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         vm.Zoom(p.X, p.Y, e.Delta > 0);
      }

      private void BlocksDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (element.IsMouseCaptured) return;
         element.CaptureMouse();
         var mainModel = ViewModel;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.SelectBlock((int)p.X, (int)p.Y);
      }

      private void BlocksMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         var mainModel = ViewModel;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.DragBlock((int)p.X, (int)p.Y);
      }

      private void BlocksUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         if (!element.IsMouseCaptured) return;
         element.ReleaseMouseCapture();
         var mainModel = ViewModel;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.ReleaseBlock((int)p.X, (int)p.Y);
      }

      private void TilesDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel.PrimaryMap.BlockEditor;
         var p = e.GetPosition(element);
         vm.TileSelectionY = (int)(p.Y * vm.TileRender.SpriteScale);
         vm.TileSelectionX = (int)(p.X * vm.TileRender.SpriteScale);
      }

      private Point GetCoordinates(FrameworkElement element, MouseEventArgs e) {
         var p = e.GetPosition(element);
         return new(p.X - element.ActualWidth / 2, p.Y - element.ActualHeight / 2);
      }

      private void ShowWarpCreation(FrameworkElement element, MouseEventArgs e) {
         element.ContextMenu.IsEnabled = true;
         element.ContextMenu.IsOpen = true;
      }

      private void DisableMenuOnClose(object sender, ContextMenuEventArgs e) {
         var menu = (ContextMenu)sender;
         menu.IsEnabled = false;
      }

      private void CreateMapForWarpClicked(object sender, RoutedEventArgs e) {
         ViewModel.CreateMapForWarp();
      }

      #endregion

      #region Border Interaction

      private void BorderDown(object sender, MouseButtonEventArgs e) => BorderMove(sender, e);

      private void BorderSelect(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.CaptureMouse();
         var p = e.GetPosition(element);
         ViewModel.ReadBorderBlock(p.X, p.Y);
      }

      private void BorderMove(object sender, MouseEventArgs e) {
         if (e.LeftButton == MouseButtonState.Pressed) {
            var element = (FrameworkElement)sender;
            var p = e.GetPosition(element);
            ViewModel.DrawBorder(p.X, p.Y);
         }
      }

      private void BorderUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         ViewModel.CompleteBorderDraw();
      }

      #endregion

      #region Shifter Interaction

      private bool withinShiftInteraction;

      private void ShifterDown(object sender, MouseButtonEventArgs e) {
         if (withinShiftInteraction) return;
         var element = (FrameworkElement)sender;
         var p = GetCoordinates(MapButtons, e);
         element.CaptureMouse();
         withinShiftInteraction = true;
         ViewModel.ShiftDown(p.X, p.Y);
      }

      private void ShifterMove(object sender, MouseEventArgs e) {
         if (!withinShiftInteraction) return;
         var p = GetCoordinates(MapButtons, e);
         ViewModel.ShiftMove(p.X, p.Y);
      }

      private void ShifterUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         if (!withinShiftInteraction) return;
         var p = GetCoordinates(MapButtons, e);
         ViewModel.ShiftUp(p.X, p.Y);
         withinShiftInteraction = false;
      }

      #endregion

      #region Event Template Interaction

      private void EventTemplateDown(object sender, MouseEventArgs e) {
         var target = (EventCreationType)((FrameworkElement)sender).Tag;
         withinMapInteraction = MouseButton.Left;
         MapView.CaptureMouse();
         ViewModel.StartEventCreationInteraction(target);
         e.Handled = true;
      }

      #endregion
   }
}
