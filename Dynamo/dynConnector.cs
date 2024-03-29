﻿//Copyright 2011 Ian Keough

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Dynamo.Controls;
using Dynamo.Elements;
using System.Diagnostics;
using Dynamo.Utilities;

namespace Dynamo.Connectors
{
    public delegate void ConnectorConnectedHandler(object sender, EventArgs e);

    public class dynConnector : UIElement
    {

        public event ConnectorConnectedHandler Connected;

        protected virtual void OnConnected(EventArgs e)
        {
            if (Connected != null)
                Connected(this, e);
        }

        const int STROKE_THICKNESS = 1;

        dynPort pStart;
        dynPort pEnd;

        PathFigure connectorPoints;
        BezierSegment connectorCurve;
        Path connector;

        double bezOffset = 100;

        //Canvas workBench;
        bool isDrawing = false;

        public bool IsDrawing
        {
            get { return isDrawing; }
        }
        public dynPort Start
        {
            get { return pStart; }
            set { pStart = value; }
        }
        public dynPort End
        {
            get { return pEnd; }
            set 
            { 
                pEnd = value; 
            }
        }
        
        public dynConnector(dynPort port, Canvas workBench, Point mousePt)
        {

            //don't allow connections to start at an input port
            if (port.PortType != PortType.INPUT)
            {
                //get start point
                //this.workBench = workBench;
                pStart = port;

                pStart.Connect(this);

                //Create a Bezier;
                connector = new Path();
                connector.Stroke = Brushes.Black;
                connector.StrokeThickness = STROKE_THICKNESS;
                connector.Opacity = .8;

                DoubleCollection dashArray = new DoubleCollection();
                dashArray.Add(5); dashArray.Add(2);
                connector.StrokeDashArray = dashArray;

                PathGeometry connectorGeometry = new PathGeometry();
                connectorPoints = new PathFigure();
                connectorCurve = new BezierSegment();

                connectorPoints.StartPoint = new Point(pStart.Center.X, pStart.Center.Y);
                connectorCurve.Point1 = connectorPoints.StartPoint;
                connectorCurve.Point2 = connectorPoints.StartPoint;
                connectorCurve.Point3 = connectorPoints.StartPoint;

                connectorPoints.Segments.Add(connectorCurve);
                connectorGeometry.Figures.Add(connectorPoints);
                connector.Data = connectorGeometry;
                workBench.Children.Add(connector);

                isDrawing = true;

                //set this to not draggable
                Dynamo.Controls.DragCanvas.SetCanBeDragged(this, false);
                Dynamo.Controls.DragCanvas.SetCanBeDragged(connector, false);

                //set the z order to the front
                Canvas.SetZIndex(this, 300);

                //register an event listener for the start port update
                //this will tell the connector to set the elements at either
                //end to be equal if pStart and pEnd are not null
                //pStart.Owner.Outputs[pStart.Index].dynElementUpdated += new Dynamo.Elements.dynElementUpdatedHandler(StartPortUpdated);
            }
            else
            {
                throw new InvalidPortException();
            }

        }

        public dynConnector(dynElement start, dynElement end, int startIndex, int endIndex, int portType)
        {
            //this.workBench = settings.WorkBench;
            
            //if (start != null && end != null && start != end)
            //{
                //in the start element, find the out port at the startIndex
                pStart = start.OutPorts[startIndex];

                dynPort endPort = null;

                if (portType == 0)
                    endPort = end.InPorts[endIndex];
                else if (portType == 1)
                    endPort = end.StatePorts[endIndex];

                //connect the two ports
                //get start point

                pStart.Connect(this);

                #region bezier creation
                connector = new Path();
                connector.Stroke = Brushes.Black;
                connector.StrokeThickness = STROKE_THICKNESS;
                connector.Opacity = .8;

                DoubleCollection dashArray = new DoubleCollection();
                dashArray.Add(5); dashArray.Add(2);
                connector.StrokeDashArray = dashArray;

                PathGeometry connectorGeometry = new PathGeometry();
                connectorPoints = new PathFigure();
                connectorCurve = new BezierSegment();

                connectorPoints.StartPoint = new Point(pStart.Center.X, pStart.Center.Y);
                connectorCurve.Point1 = connectorPoints.StartPoint;
                connectorCurve.Point2 = connectorPoints.StartPoint;
                connectorCurve.Point3 = connectorPoints.StartPoint;

                connectorPoints.Segments.Add(connectorCurve);
                connectorGeometry.Figures.Add(connectorPoints);
                connector.Data = connectorGeometry;
                dynElementSettings.SharedInstance.Workbench.Children.Add(connector);
   
                #endregion

                isDrawing = true;

                //set this to not draggable
                Dynamo.Controls.DragCanvas.SetCanBeDragged(this, false);
                Dynamo.Controls.DragCanvas.SetCanBeDragged(connector, false);

                //set the z order to the front
                Canvas.SetZIndex(this, 300);

                this.Connect(endPort);

            //}
            
        }

        void StartPortUpdated(object sender, EventArgs e)
        {

            if (pEnd != null)
            {
                if (pEnd.Owner != null)
                {
                    //set the end equal to the start
                    pEnd.Owner.InPortData[pEnd.Index].Object = pStart.Owner.OutPortData[pStart.Index].Object;

                    //tell the end to update
                    pEnd.Owner.Update();
                }
            }

        }

        public void SendMessage()
        {

            if (pEnd != null)
            {
                if (pEnd.Owner != null)
                {
                    if(pEnd.PortType == PortType.INPUT)
                        pEnd.Owner.InPortData[pEnd.Index].Object = pStart.Owner.OutPortData[pStart.Index].Object;
                    else if(pEnd.PortType == PortType.STATE)
                        pEnd.Owner.StatePortData[pEnd.Index].Object = pStart.Owner.OutPortData[pStart.Index].Object;
                    
                    //tell the end port's ownder to update
                    //pEnd.Owner.Update();
                }
            }

        }
        
        public void Redraw(Point p2)
        {
            if(isDrawing)
            {
                if (pStart != null)
                {
                    connectorPoints.StartPoint = pStart.Center;
                    connectorCurve.Point1 = new Point(pStart.Center.X + bezOffset, pStart.Center.Y);
                    connectorCurve.Point2 = new Point(p2.X - bezOffset, p2.Y);
                    connectorCurve.Point3 = p2;
                }

            }
        }

        public bool Connect(dynPort p)
        {
            //test if the port that you are connecting too is not the start port or the end port
            //of the current connector
            if (p.Equals(pStart) || p.Equals(pEnd))
            {
                return false;
            }

            //if the selected connector is also an output connector, return false
            //output ports can't be connected to eachother
            if (p.PortType == PortType.OUTPUT)
            {
                return false;
            }

            //test if the port that you are connecting to is an input and 
            //already has other connectors
            if (p.PortType == PortType.INPUT && p.Connectors.Count > 0)
            {
                return false;
            }

            //test if the port element at B can connect to the port at A
            //test if you can convert the element at A to the element at b
            if (p.PortType == PortType.INPUT)
            {
                if (!p.Owner.InPortData[p.Index].PortType.IsAssignableFrom(pStart.Owner.OutPortData[pStart.Index].PortType))
                {
                    return false;
                }
            }
            else if (p.PortType == PortType.STATE)
            {
                if (!p.Owner.StatePortData[p.Index].PortType.IsAssignableFrom(pStart.Owner.OutPortData[pStart.Index].PortType))
                {
                    return false;
                }
            }

            //turn the line solid
            connector.StrokeDashArray.Clear();

            pEnd = p;

            if (pEnd != null)
            {
                //set the start and end values to equal so this 
                //starts evaulating immediately
                pEnd.Owner.InPortData[p.Index].Object = pStart.Owner.OutPortData[pStart.Index].Object;
                p.Connect(this);    
                pEnd.Update();
                pEnd.Owner.Update();
            }
            
            return true;
        }

        public void Disconnect(dynPort p)
        {
            if (p.Equals(pStart))
            {
                //pStart.Owner.Outputs[pStart.Index] = null;
                pStart = null;
            }
            
            if (p.Equals(pEnd))
            {
                if(pEnd.PortType == PortType.INPUT)
                    pEnd.Owner.InPortData[pEnd.Index].Object = null;
                else if(pEnd.PortType == PortType.STATE)
                    pEnd.Owner.StatePortData[pEnd.Index].Object = null;
                pEnd = null;
            }

            p.Disconnect(this);

            //turn the connector back to dashed
            connector.StrokeDashArray.Add(5);
            connector.StrokeDashArray.Add(2);
        }

        public void Kill()
        {
            if (pStart != null && pStart.Connectors.Contains(this))
            {
                pStart.Disconnect(this);
                //pStart.Connectors.Remove(this);
                //do not remove the owner's output element
            }
            if (pEnd!= null && pEnd.Connectors.Contains(this))
            {
                pEnd.Disconnect(this);
                //remove the reference to the
                //dynElement attached to port A
                pEnd.Owner.InPortData[pEnd.Index].Object = null;

            }

            pStart = null;
            pEnd = null;

            dynElementSettings.SharedInstance.Workbench.Children.Remove(connector);

            isDrawing = false;
        }

        public void Redraw()
        {
            //don't redraw with null end points;
            if (pStart != null)
            {
                connectorPoints.StartPoint = pStart.Center;
                connectorCurve.Point1 = new Point(pStart.Center.X + bezOffset, pStart.Center.Y);
            }
            if(pEnd != null)
            {
                if (pEnd.PortType == PortType.INPUT)
                {
                    connectorCurve.Point2 = new Point(pEnd.Center.X - bezOffset, pEnd.Center.Y);
                }
                else if (pEnd.PortType == PortType.STATE)
                {
                    connectorCurve.Point2 = new Point(pEnd.Center.X, pEnd.Center.Y + bezOffset);
                }
                connectorCurve.Point3 = pEnd.Center;
            }
        }

        public dynElement FindDynElementByGuid(Guid guid)
        {
            foreach (UIElement uiel in dynElementSettings.SharedInstance.Workbench.Children)
            {
                dynElement testEl = null;

                //walk up through the inheritance to find whether the base type is a dynElement
                Type startType = uiel.GetType();
                while (startType.BaseType != null)
                {
                    startType = startType.BaseType;
                    if (startType == typeof(dynElement))
                    {
                        testEl = uiel as dynElement;
                        break;
                    }
                }

                if (testEl != null)
                {
                    if (testEl.GUID == guid)
                    {
                        return testEl;
                    }
                }
            }

            return null;
        }
    }

    public class InvalidPortException : ApplicationException
    {
        private string message;
        public override string Message
        {
            get { return message; }
        }

        public InvalidPortException()
        {
            message = "Connection port is not valid.";
        }

    }
}
