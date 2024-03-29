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
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.IO;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Events;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using TextBox = System.Windows.Controls.TextBox;
using System.Windows.Forms;
using Dynamo.Controls;
using Dynamo.Connectors;
using Dynamo.Utilities;
using System.IO.Ports;

namespace Dynamo.Elements
{
    public abstract class dynReferencePoint:dynElement,IDynamic
    {
        public dynReferencePoint()
        {

            OutPortData.Add(new PortData(null, "pt", "The Reference Point(s) created from this operation.", typeof(dynReferencePoint)));
            OutPortData[0].Object = this.Tree;

        }

        public override void Draw()
        {

        }

        public override void Destroy()
        {
            //base destroys all elements in the collection
            base.Destroy();
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }
    }

    [ElementName("ReferencePointByXYZ")]
    [ElementDescription("An element which creates a reference point.")]
    [RequiresTransaction(true)]
    public class dynReferencePointByXYZ : dynReferencePoint, IDynamic
    {
        public dynReferencePointByXYZ()
        {
            InPortData.Add(new PortData(null, "xyz", "The point(s) from which to create reference points.", typeof(dynXYZ)));

            base.RegisterInputsAndOutputs();
        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree a = InPortData[0].Object as DataTree;
                if (a != null)
                {
                    Process(this.Tree.Trunk, a.Trunk);
                }

            }
        }

        public void Process(DataTreeBranch currBranch, DataTreeBranch a)
        {
            foreach (object o in a.Leaves)
            {
                XYZ pt = o as XYZ;
                if (pt != null)
                {
                    ReferencePoint rp = dynElementSettings.SharedInstance.Doc.Document.FamilyCreate.NewReferencePoint(pt);
                    currBranch.Leaves.Add(rp);
                    Elements.Append(rp);
                }
            }

            foreach (DataTreeBranch aChild in a.Branches)
            {
                DataTreeBranch subBranch = new DataTreeBranch();
                currBranch.Branches.Add(subBranch);

                Process(subBranch, aChild);
            }
        }
        public override void Destroy()
        {
            //base destroys all elements in the collection
            base.Destroy();
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }
    }

    [ElementName("ReferencePointGridXYZ")]
    [ElementDescription("An element which creates a grid of reference points.")]
    [RequiresTransaction(true)]
    public class dynReferencePtGrid : dynReferencePoint, IDynamic
    {
        public dynReferencePtGrid()
        {
            InPortData.Add(new PortData(null, "xi", "Number in the X direction.", typeof(dynInt)));
            InPortData.Add(new PortData(null, "yi", "Number in the Y direction.", typeof(dynInt)));
            InPortData.Add(new PortData(null, "pt", "Origin.", typeof(dynReferencePoint)));
            InPortData.Add(new PortData(null, "x", "The X spacing.", typeof(dynDouble)));
            InPortData.Add(new PortData(null, "y", "The Y spacing.", typeof(dynDouble)));
            InPortData.Add(new PortData(null, "z", "The Z offset.", typeof(dynDouble)));

            //outports already added in parent

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree xyzTree = InPortData[2].Object as DataTree;
                if (xyzTree != null)
                {
                    Process(xyzTree.Trunk, this.Tree.Trunk);
                }
            }
        }

        public void Process(DataTreeBranch bIn, DataTreeBranch currentBranch)
        {

            //use each XYZ leaf on the input
            //to define a new origin
            foreach (object o in bIn.Leaves)
            {
                ReferencePoint rp = o as ReferencePoint;

                if (rp != null)
                {
                    for (int i = 0; i < (int)InPortData[0].Object; i++)
                    {
                        //create a branch for the data tree for
                        //this row of points
                        DataTreeBranch b = new DataTreeBranch();
                        currentBranch.Branches.Add(b);

                        for (int j = 0; j < (int)InPortData[1].Object; j++)
                        {
                            XYZ pt = new XYZ(rp.Position.X + i * (double)InPortData[3].Object,
                                rp.Position.Y + j * (double)InPortData[4].Object,
                                rp.Position.Z);

                            ReferencePoint rpNew = dynElementSettings.SharedInstance.Doc.Document.FamilyCreate.NewReferencePoint(pt);

                            //add the point as a leaf on the branch
                            b.Leaves.Add(rpNew);

                            //add the element to the collection
                            Elements.Append(rpNew);
                        }
                    }
                }
            }

            foreach (DataTreeBranch b1 in bIn.Branches)
            {
                DataTreeBranch newBranch = new DataTreeBranch();
                currentBranch.Branches.Add(newBranch);

                Process(b1, newBranch);
            }

        }

        public override void Destroy()
        {
            //base destroys all elements in the collection
            base.Destroy();
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }
    }

    [ElementName("Distance to Ref. Pt.")]
    [ElementDescription("An element which measures a distance between reference point(s).")]
    [RequiresTransaction(false)]
    public class dynDistanceBetweenPoints : dynElement, IDynamic
    {

        public dynDistanceBetweenPoints()
        {
            InPortData.Add(new PortData(null, "pts.", "A group of Reference point(s).", typeof(dynReferencePoint)));
            InPortData.Add(new PortData(null, "pt.", "A Reference point.", typeof(dynReferencePoint)));

            OutPortData.Add(new PortData(null, "Distance", "Distance(s) between points.", typeof(dynDouble)));
            OutPortData[0].Object = this.Tree;

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree treeA = InPortData[0].Object as DataTree;
                DataTree treeB = InPortData[1].Object as DataTree;

                if (treeB != null && treeB.Trunk.Leaves.Count > 0)
                {
                    //we're only using the first point in the tree right now.
                    if (treeB.Trunk.Leaves.Count > 0)
                    {
                        ReferencePoint pt = treeB.Trunk.Leaves[0] as ReferencePoint;


                        if (treeA != null && pt != null)
                        {
                            Process(treeA.Trunk, this.Tree.Trunk);
                        }
                    }
                }
            }
        }

        public void Process(DataTreeBranch bIn, DataTreeBranch currentBranch)
        {
            DataTree dt = InPortData[1].Object as DataTree;
            ReferencePoint attractor = dt.Trunk.Leaves[0] as ReferencePoint;

            //use each XYZ leaf on the input
            //to define a new origin
            foreach (object o in bIn.Leaves)
            {
                ReferencePoint rp = o as ReferencePoint;

                if (rp != null)
                {
                    //get the distance betweent the points
                    
                    double dist = rp.Position.DistanceTo(attractor.Position);
                    currentBranch.Leaves.Add(dist);
                }
            }

            foreach (DataTreeBranch b1 in bIn.Branches)
            {
                DataTreeBranch newBranch = new DataTreeBranch();
                currentBranch.Branches.Add(newBranch);

                Process(b1, newBranch);
            }

        }

        public override void Destroy()
        {
            //base destroys all elements in the collection
            base.Destroy();
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }
    }
    //[ElementName("PtOnEdge")]
    //[ElementDescription("Create an element which owns a reference point on a selected edge.")]
    //[RequiresTransaction(true)]
    //public class dynPointOnEdge : dynElement, IDynamic
    //{
    //    public dynPointOnEdge(string nickName)
    //        : base(nickName)
    //    {
    //        InPortData.Add(new PortData(null, "cv", "ModelCurve", typeof(ModelCurve)));
    //        InPortData.Add(new PortData(null, "t", "Parameter on edge.", typeof(double)));
    //        OutPortData.Add(new PortData(null, "pt", "PointOnEdge", typeof(dynPointOnEdge)));

    //        base.RegisterInputsAndOutputs();
    //    }

    //    public override void Draw()
    //    {
    //        if (CheckInputs())
    //        {

    //            Reference r = (InPortData[0].Object as ModelCurve).GeometryCurve.Reference;
    //            OutPortData[0].Object = dynElementSettings.SharedInstance.Doc.Application.Application.Create.NewPointOnEdge(r, (double)InPortData[1].Object);

    //        }
    //    }

    //    public override void Destroy()
    //    {
    //        base.Destroy();
    //    }

    //    public override void Update()
    //    {
    //        OnDynElementReadyToBuild(EventArgs.Empty);
    //    }
    //}
}
