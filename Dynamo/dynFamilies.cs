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
    [ElementName("Family Type Selector")]
    [ElementDescription("An element which allows you to select a Family Type from a drop down list.")]
    [RequiresTransaction(true)]
    public class dynFamilyTypeSelector : dynElement, IDynamic
    {
        TextBox tb;
        System.Windows.Controls.ComboBox combo;
        Hashtable comboHash;
        FamilySymbol fs;

        public FamilySymbol SelectedFamilySymbol
        {
            get { return fs; }
        }

        public dynFamilyTypeSelector()
        {

            //widen the control
            this.topControl.Width = 300;

            //add a drop down list to the window
            combo = new System.Windows.Controls.ComboBox();
            combo.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            combo.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            this.inputGrid.Children.Add(combo);
            System.Windows.Controls.Grid.SetColumn(combo, 0);
            System.Windows.Controls.Grid.SetRow(combo, 0);

            combo.SelectionChanged += new SelectionChangedEventHandler(combo_SelectionChanged);
            combo.DropDownOpened += new EventHandler(combo_DropDownOpened);
            comboHash = new Hashtable();

            PopulateComboBox();

            OutPortData.Add(new PortData(null, "", "Family type.", typeof(dynFamilyTypeSelector)));
            base.RegisterInputsAndOutputs();
        }

        void combo_DropDownOpened(object sender, EventArgs e)
        {
            PopulateComboBox();
        }

        private void PopulateComboBox()
        {
            comboHash.Clear();
            combo.Items.Clear();

            //load all the currently loaded types into the combo list
            FilteredElementCollector fec = new FilteredElementCollector(dynElementSettings.SharedInstance.Doc.Document);
            fec.OfClass(typeof(Family));
            foreach (Family f in fec.ToElements())
            {
                foreach (FamilySymbol fs in f.Symbols)
                {
                    ComboBoxItem cbi = new ComboBoxItem();
                    string comboText = f.Name + ":" + fs.Name;
                    cbi.Content = comboText;
                    combo.Items.Add(cbi);
                    comboHash.Add(comboText, fs);
                }
            }
        }

        void combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cbi = combo.SelectedItem as ComboBoxItem;

            if (cbi != null)
            {
                fs = comboHash[cbi.Content] as FamilySymbol;
                OutPortData[0].Object = fs;

                OnDynElementReadyToBuild(EventArgs.Empty);
            }
        }

        public override void Update()
        {
            //tb.Text = OutPortData[0].Object.ToString();
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

    }

    [ElementName("Instance Parameter Mapper")]
    [ElementDescription("An element which maps the parameters of a Family Type.")]
    [RequiresTransaction(true)]
    public class dynInstanceParameterMapper : dynElement, IDynamic
    {
        //Hashtable parameterMap = new Hashtable();
        SortedDictionary<string, object> parameterMap = new SortedDictionary<string, object>();

        //public Hashtable ParameterMap
        //{
        //    get { return parameterMap; }
        //}

        public SortedDictionary<string, object> ParameterMap
        {
            get { return parameterMap; }
        }

        public dynInstanceParameterMapper()
        {


            this.topControl.Width = 300;

            InPortData.Add(new PortData(null, "fi", "The family instance(s) to map.", typeof(dynFamilyInstanceCreator)));
            OutPortData.Add(new PortData(null, "", "A map of parameter values on the instance.", typeof(dynInstanceParameterMapper)));
            OutPortData[0].Object = parameterMap;

            //add a button to the inputGrid on the dynElement
            System.Windows.Controls.Button paramMapButt = new System.Windows.Controls.Button();
            this.inputGrid.Children.Add(paramMapButt);
            paramMapButt.Margin = new System.Windows.Thickness(0, 0, 0, 0);
            paramMapButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            paramMapButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            paramMapButt.Click += new System.Windows.RoutedEventHandler(paramMapButt_Click);
            paramMapButt.Content = "Map";
            paramMapButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            paramMapButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            base.RegisterInputsAndOutputs();

        }

        void paramMapButt_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (CheckInputs())
            {
                CleanupOldPorts();

                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    object o = treeIn.Trunk.FindFirst();

                    if (o != null)
                    {
                        FamilyInstance fi = o as FamilyInstance;
                        if (fi != null)
                        {
                            MapPorts(fi);
                        }
                    }
                }
            }
        }

        private void MapPorts(FamilyInstance fi)
        {
            parameterMap.Clear();

            foreach (Parameter p in fi.Parameters)
            {
                if (!p.IsReadOnly)  //don't want it if it is read only
                {
                    if (p.StorageType == StorageType.Double)
                    {
                        string paramName = p.Definition.Name;

                        PortData pd = new PortData(null, 
                            p.Definition.Name[0].ToString(), 
                            paramName, 
                            typeof(dynDouble));
                        InPortData.Add(pd);
                        parameterMap.Add(paramName, pd.Object);
                    }
                }
            }

            //add back new ports
            for (int i = 1; i < InPortData.Count; i++)
            {
                dynElement el = InPortData[i].Object as dynElement;

                RowDefinition rd = new RowDefinition();
                gridLeft.RowDefinitions.Add(rd);

                //add a port for each input
                //distribute the ports along the 
                //edges of the icon
                AddPort(el, PortType.INPUT, InPortData[i].NickName, i);
            }

            //resize this thing
            base.ResizeElementForInputs();

            base.SetToolTips();
        }

        private void CleanupOldPorts()
        {

            //clear all the inputs but the first one
            //which is the family instance
            //first kill all the connectors
            for (int i = 1; i < InPortData.Count; i++)
            {
                dynPort p = InPorts[i];

                //must remove the connectors iteratively
                //do not use a foreach here!
                while (p.Connectors.Count > 0)
                {
                    dynConnector c = p.Connectors[p.Connectors.Count - 1] as dynConnector;
                    c.Kill();
                }
            }

            //then remove all the ports
            while (InPorts.Count > 1)
            {
                InPorts.RemoveAt(InPorts.Count - 1);
                InPortData.RemoveAt(InPortData.Count - 1);
            }

            while (gridLeft.Children.Count > 2)
            {
                //remove the port from the children list
                gridLeft.Children.RemoveAt(gridLeft.Children.Count - 1);
            }

            while (gridLeft.RowDefinitions.Count > 1)
            {
                gridLeft.RowDefinitions.RemoveAt(gridLeft.RowDefinitions.Count - 1);
                
            }

        }

        public override void Draw()
        {

            //skip the first port data because it's the family instances
            for(int i=1; i<InPortData.Count; i++)
            {
                PortData pd = InPortData[i];

                if (pd.Object != null)
                {
                    //parameter value keys are the tooltip - the name 
                    //of the parameter
                    //set the objects on the parameter map
                    parameterMap[pd.ToolTipString] = pd.Object;

                    DataTree familyInstTree = InPortData[0].Object as DataTree;

                    if (familyInstTree != null)
                    {
                        if (pd.Object.GetType() == typeof(DataTree))
                        {
                            DataTree doubleTree = pd.Object as DataTree;
                            //get the parameter represented by the port data
                            Process(familyInstTree.Trunk, doubleTree.Trunk, pd.ToolTipString);
                        }
                        else
                        {
                            double d = Convert.ToDouble(pd.Object);
                            Process(familyInstTree.Trunk, d, pd.ToolTipString);
                        }
                    }
                }

            }

        }

        public void Process(DataTreeBranch familyBranch, DataTreeBranch doubleBranch, string paramName)
        {
            int leafCount = 0;
            foreach(object o in familyBranch.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    Parameter p = fi.get_Parameter(paramName);
                    if(p!= null)
                    {
                        p.Set(Convert.ToDouble(doubleBranch.Leaves[leafCount]));
                        dynElementSettings.SharedInstance.Doc.RefreshActiveView();
                    }
                }
                leafCount++;
            }

            int subBranchCount = 0;
            foreach (DataTreeBranch nextBranch in familyBranch.Branches)
            {
                //don't do this if the double tree doesn't
                //have a member in the same location
                if (doubleBranch.Branches.Count-1 >= subBranchCount)
                {
                    Process(nextBranch, doubleBranch.Branches[subBranchCount], paramName);
                }
                subBranchCount++;
            }
        }

        public void Process(DataTreeBranch familyBranch, double d, string paramName)
        {
            int leafCount = 0;
            foreach (object o in familyBranch.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    Parameter p = fi.get_Parameter(paramName);
                    if (p != null)
                    {
                        p.Set(d);
                        dynElementSettings.SharedInstance.Doc.RefreshActiveView();
                    }
                }
                leafCount++;
            }

            foreach (DataTreeBranch nextBranch in familyBranch.Branches)
            {
                //don't do this if the double tree doesn't
                //have a member in the same location

                Process(nextBranch, d, paramName);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }

    [ElementName("Family Instance Creator")]
    [ElementDescription("An element which allows you to create family instances from a set of points.")]
    [RequiresTransaction(true)]
    public class dynFamilyInstanceCreator : dynElement, IDynamic
    {

        public dynFamilyInstanceCreator()
        {

            InPortData.Add(new PortData(null, "pt", "Reference points.", typeof(dynReferencePoint)));
            InPortData.Add(new PortData(null, "typ", "The Family Symbol to use for instantiation.", typeof(dynFamilyTypeSelector)));

            //StatePortData.Add(new PortData(null, "map", "Instance parameter map.", typeof(dynInstanceParameterMapper)));

            OutPortData.Add(new PortData(null, "fi", "Family instances created by this operation.", typeof(dynFamilyInstanceCreator)));
            OutPortData[0].Object = this.Tree;

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    Process(treeIn.Trunk, this.Tree.Trunk);
                }
            }

            base.Draw();
        }

        public void Process(DataTreeBranch bIn, DataTreeBranch currentBranch)
        {

            foreach (object o in bIn.Leaves)
            {
                ReferencePoint rp = o as ReferencePoint;

                if (rp != null)
                {
                    //get the location of the point
                    XYZ pos = rp.Position;
                    FamilySymbol fs = InPortData[1].Object as FamilySymbol;
                    FamilyInstance fi = dynElementSettings.SharedInstance.Doc.Document.FamilyCreate.NewFamilyInstance(pos, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    Elements.Append(fi);
                    currentBranch.Leaves.Add(fi);

                }
            }

            foreach (DataTreeBranch b1 in bIn.Branches)
            {
                DataTreeBranch newBranch = new DataTreeBranch();
                this.Tree.Trunk.Branches.Add(newBranch);
                Process(b1, newBranch);
            }
        }

        public void ProcessState(DataTreeBranch bIn, Hashtable parameterMap)
        {
            foreach (object o in bIn.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    foreach (DictionaryEntry de in parameterMap)
                    {
                        if (de.Value != null)
                        {
                            //find the parameter on the family instance
                            Parameter p = fi.get_Parameter(de.Key.ToString());
                            if (p != null)
                            {
                                if (de.Value != null)
                                    p.Set((double)de.Value);
                            }
                        }
                    }
                }
            }

            foreach (DataTreeBranch nextBranch in bIn.Branches)
            {
                ProcessState(nextBranch, parameterMap);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }

    [ElementName("Family Instance Parameter Evaluation")]
    [ElementDescription("An element which allows you to modify parameters on family instances.")]
    [RequiresTransaction(true)]
    public class dynFamilyInstanceParameterEvaluation: dynElement, IDynamic
    {

        public dynFamilyInstanceParameterEvaluation()
        {

            InPortData.Add(new PortData(null, "fi", "Family instances.", typeof(dynFamilyInstanceCreator)));
            InPortData.Add(new PortData(null, "map", "Parameter map.", typeof(dynInstanceParameterMapper)));

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    //Hashtable parameterMap = InPortData[1].Object as Hashtable;

                    SortedDictionary<string, object> parameterMap = InPortData[1].Object as SortedDictionary<string, object>;

                    if (parameterMap != null)
                        ProcessState(treeIn.Trunk, parameterMap);
                }
            }

            base.Draw();
        }

        private void ProcessState(DataTreeBranch bIn, SortedDictionary<string,object> parameterMap)
        {
            foreach (object o in bIn.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    //foreach (DictionaryEntry de in parameterMap)
                    foreach (KeyValuePair<string,object> de in parameterMap)
                    {
                        if (de.Value != null)
                        {
                            //find the parameter on the family instance
                            Parameter p = fi.get_Parameter(de.Key.ToString());
                            if (p != null)
                            {
                                if (de.Value != null)
                                    p.Set((double)de.Value);
                            }
                        }
                    }
                }
            }

            foreach (DataTreeBranch nextBranch in bIn.Branches)
            {
                ProcessState(nextBranch, parameterMap);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }
}
