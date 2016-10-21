﻿using System.IO;
using System.Xml;
using Caliburn.Micro;
using Simion;
using System.Runtime.Serialization.Formatters.Binary;
using System;

namespace Badger.ViewModels
{
    abstract public class ConfigNodeViewModel: PropertyChangedBase
    {
        //access to the root node
        protected AppViewModel m_appViewModel;
        public XmlNode nodeDefinition;

        protected string m_default = "";
        private string m_content = "";
        public string content
        {
            get { return m_content; }
            set {
                m_content = value;
                bIsValid= validate();
                NotifyOfPropertyChange(() => content);
            }
        }

        private string m_textColor = XMLConfig.colorDefaultValue;
        public string textColor { get { return m_textColor; }
            set { m_textColor = value; NotifyOfPropertyChange(() => textColor); } }

        abstract public bool validate();

        //Comment
        private string m_comment= "";
        public string comment { get { return m_comment; } set { m_comment = value; } }

        //Validation
        private bool m_bIsValid = false;
        public bool bIsValid
        {
            get { return m_bIsValid; }
            set
            {
                m_bIsValid = value;
                if (m_bIsValid) textColor = XMLConfig.colorValidValue;
                else textColor = XMLConfig.colorInvalidValue;
                NotifyOfPropertyChange(() => bIsValid);
            }
        }

        public void forkThisNode()
        {
            if (m_parent != null)
            {
                m_parent.forkChild(this);
                System.Console.WriteLine("Forked node: " + name);
            }
            else System.Console.WriteLine("Can't fork this node because it has no parent: " + name);
        }
        virtual public void forkChild(ConfigNodeViewModel forkedChild)
        {
            System.Console.WriteLine("Error: non-nested config node asked to forka child");
        }


        //clone
        public ConfigNodeViewModel clone()
        {
            return getInstance(m_appViewModel, m_parent, nodeDefinition, "");
        }

        //XML output methods
        public virtual void outputXML(StreamWriter writer,string leftSpace)
        {
            writer.Write( leftSpace + "<" + name + ">" + content + "</" + name + ">\n");
        }

        //XPath methods
        protected string m_xPath;
        public string xPath { get{ return m_xPath; }  set { m_xPath = value; } }

        //Name
        private string m_name;
        public string name { get { return m_name; } set { m_name = value; } }

        //Parent
        protected ConfigNodeViewModel m_parent;
        
        //Initialization stuff common to all types of configuration nodes
        protected void commonInit(AppViewModel appViewModel,ConfigNodeViewModel parent, XmlNode definitionNode, string parentXPath)
        {
            m_parent = parent;
            m_appViewModel = appViewModel;
            nodeDefinition = definitionNode;
            name = definitionNode.Attributes[XMLConfig.nameAttribute].Value;
            xPath = parentXPath + "/" + name;
            if (definitionNode.Attributes.GetNamedItem(XMLConfig.defaultAttribute)!=null)
            {
                m_default = definitionNode.Attributes[XMLConfig.defaultAttribute].Value;
            }
            if (definitionNode.Attributes.GetNamedItem(XMLConfig.commentAttribute) != null)
            {
                comment = definitionNode.Attributes[XMLConfig.commentAttribute].Value;
            }
            //System.Console.WriteLine("loading " + name + ". XPath=" + m_xPath);
        }


        //FACTORY
        public static ConfigNodeViewModel getInstance(AppViewModel appDefinition,ConfigNodeViewModel parent,XmlNode definitionNode, string parentXPath, XmlNode configNode= null)
        {
            switch (definitionNode.Name)
            {
                case XMLConfig.integerNodeTag: return new IntegerValueConfigViewModel(appDefinition, parent, definitionNode,parentXPath,configNode);
                case XMLConfig.doubleNodeTag: return new DoubleValueConfigViewModel(appDefinition, parent, definitionNode, parentXPath,configNode);
                case XMLConfig.stringNodeTag: return new StringValueConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
                case XMLConfig.filePathNodeTag: return new FilePathValueConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
                case XMLConfig.dirPathNodeTag: return new DirPathValueConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
                case XMLConfig.xmlRefNodeTag: return new XmlDefRefValueConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);

                case XMLConfig.branchNodeTag: return new BranchConfigViewModel(appDefinition, parent, definitionNode,parentXPath,configNode);
                case XMLConfig.choiceNodeTag: return new ChoiceConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
                case XMLConfig.choiceElementNodeTag: return new ChoiceElementConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
                case XMLConfig.enumNodeTag: return new EnumeratedValueConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
                case XMLConfig.multiValuedNodeTag: return new MultiValuedConfigViewModel(appDefinition, parent, definitionNode, parentXPath, configNode);
            }

            return null;
        }

    }
    abstract public class NestedConfigNode : ConfigNodeViewModel
    {
        //Children
        protected BindableCollection<ConfigNodeViewModel> m_children = new BindableCollection<ConfigNodeViewModel>();
        public BindableCollection<ConfigNodeViewModel> children { get { return m_children; }
            set { m_children = value; NotifyOfPropertyChange(() => children); } }

        public override void outputXML(StreamWriter writer, string leftSpace)
        {
            //System.Console.WriteLine("Exporting " + name);
            writer.Write(leftSpace + getXMLHeader());
            outputChildrenXML(writer, leftSpace + "  ");
            writer.Write(leftSpace + getXMLFooter());
        }

        public void outputChildrenXML(StreamWriter writer, string leftSpace)
        {
            foreach (ConfigNodeViewModel child in m_children)
                child.outputXML(writer, leftSpace);
        }
        public virtual string getXMLHeader() { return "<" + name + ">\n"; }
        public virtual string getXMLFooter() { return "</" + name + ">\n"; }

        protected void childrenInit(AppViewModel appViewModel, XmlNode classDefinition
            , string parentXPath, XmlNode configNode = null)
        {
            if (classDefinition != null)
            {
                foreach (XmlNode child in classDefinition.ChildNodes)
                {
                    ConfigNodeViewModel childNode;
                    if (isChildForked(child.Attributes[XMLConfig.nameAttribute].Value, configNode))
                    {
                        children.Add(new ForkedNodeViewModel(appViewModel, child, configNode));
                    }
                    else
                    {
                        childNode = ConfigNodeViewModel.getInstance(appViewModel, this, child, parentXPath, configNode);
                        if (childNode != null)
                            children.Add(childNode);
                    }
                }
            }
        }

        private bool isChildForked(string childName, XmlNode configNode)
        {
            if (configNode == null)
                return false;
            foreach(XmlNode configChildNode in configNode)
            {
                if (configChildNode.Name == XMLConfig.forkedNodeTag
                    && configChildNode.Attributes[XMLConfig.nameAttribute].Value == childName)
                    return true;
            }
            return false;
        }
        public override bool validate()
        {
            bIsValid = true;
            foreach (ConfigNodeViewModel child in children)
            {
                if (!child.validate())
                {
                    bIsValid = false;
                }
            }
            return bIsValid;
        }

        //FORKS
        public override void forkChild(ConfigNodeViewModel forkedChild)
        {
            ForkedNodeViewModel newForkNode;
            ForkViewModel newFork;
            if (m_appViewModel!=null)
            {
                //cross-reference
                newForkNode = new ForkedNodeViewModel(m_appViewModel,forkedChild);
                newFork= m_appViewModel.addFork(forkedChild, newForkNode);
                newForkNode.fork = newFork;
                if (newFork != null)
                {
                    int oldIndex = children.IndexOf(forkedChild);
                    children.Remove(forkedChild);
                    children.Insert(oldIndex, newForkNode);
                }
            }
        }
    }


}
