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

        public dynFamilyTypeSelector(string nickName)
            : base(nickName)
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
    [RequiresTransaction(false)]
    public class dynInstanceParameterMapper : dynElement, IDynamic
    {
        Hashtable parameterMap = new Hashtable();

        public Hashtable ParameterMap
        {
            get { return parameterMap; }
        }

        public dynInstanceParameterMapper(string nickName)
            : base(nickName)
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
            //read from the state objects
            if (CheckInputs())
            {
                //if (OutPorts[0].Connectors.Count > 0)
                //{
                    //read the end of this connector
                    //DataTree treeIn = OutPorts[0].Connectors[0].End.Owner.Tree;
                    DataTree treeIn = InPortData[0].Object as DataTree;
                    if (treeIn != null)
                    {
                        Process(treeIn.Trunk);
                    }
                //}
            }
        }

        void Process(DataTreeBranch currentBranch)
        {
            foreach (object o in currentBranch.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;

                //do not check all inputs here as we don't care
                //they're going to get dropped anyway
                //only check the first inport.
                if (fi != null)
                {
                    parameterMap.Clear();

                    #region cleanup the old ports
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

                    while (gridLeft.RowDefinitions.Count > 1)
                    {
                        //remove the port from the children list
                        gridLeft.Children.RemoveAt(gridLeft.RowDefinitions.Count - 1);
                        portNamesLeft.Children.RemoveAt(portNamesLeft.RowDefinitions.Count - 1);

                        gridLeft.RowDefinitions.RemoveAt(gridLeft.RowDefinitions.Count - 1);
                        portNamesLeft.RowDefinitions.RemoveAt(portNamesLeft.RowDefinitions.Count - 1);
                    }
                    #endregion

                    foreach (Parameter p in fi.Parameters)
                    {
                        if (!p.IsReadOnly)  //don't want it if it is read only
                        {
                            if (p.StorageType == StorageType.Double)
                            {
                                string paramName = p.Definition.Name;

                                PortData pd = new PortData(null, p.Definition.Name[0].ToString(), paramName, typeof(dynDouble));
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

                        RowDefinition nameRd = new RowDefinition();
                        portNamesLeft.RowDefinitions.Add(nameRd);

                        //add a port for each input
                        //distribute the ports along the 
                        //edges of the icon
                        AddPort(el, PortType.INPUT, InPortData[i].NickName, i);
                    }

                    //resize this thing
                    base.ResizeElementForInputs();

                    base.SetToolTips();

                    //OnDynElementReadyToBuild(EventArgs.Empty);
                }

                //we only want to process the first thing
                break;
            }
            foreach (DataTreeBranch childBranch in currentBranch.Branches)
            {
                Process(childBranch);
            }
        }

        public override void Draw()
        {
            //get the input value and map and map it
            //to the parameter value
            //if (CheckInputs())
            //{
                foreach(PortData pd in InPortData)
                {
                    //parameter value keys are the tooltip - the name 
                    //of the parameter
                    parameterMap[pd.ToolTipString] = pd.Object;
                }
                //OnDynElementReadyToBuild(EventArgs.Empty);
            //}
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

        public dynFamilyInstanceCreator(string nickName)
            : base(nickName)
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

                    //Hashtable parameterMap = StatePortData[0].Object as Hashtable;
                    //if(parameterMap != null)
                    //    ProcessState(this.Tree.Trunk, parameterMap);
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

                    //Hashtable parameterMap = StatePortData[0].Object as Hashtable;
                    //if (parameterMap != null)
                    //{
                    //    foreach (DictionaryEntry de in parameterMap)
                    //    {
                    //        //find the parameter on the family instance
                    //        Parameter p = fi.Symbol.get_Parameter(de.Key.ToString());
                    //        if (p != null)
                    //        {
                    //            p.Set((double)de.Value);
                    //        }
                    //    }
                    //}
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

        public dynFamilyInstanceParameterEvaluation(string nickName)
            : base(nickName)
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
                    Hashtable parameterMap = InPortData[1].Object as Hashtable;
                    if (parameterMap != null)
                        ProcessState(treeIn.Trunk, parameterMap);
                }
            }

            base.Draw();
        }

        private void ProcessState(DataTreeBranch bIn, Hashtable parameterMap)
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
}