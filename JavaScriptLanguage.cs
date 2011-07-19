// -----------------------------------------------------------
// JavaScript Language view for Lutz Roeder's .NET Reflector
// Copyright (C) 2011 Frank A. Krueger. All rights reserved.
// fak@praeclarum.org
//
// based on DelphiLanguage from Lutz Roeder
// ----------------------------------------

namespace Reflector.Application.Languages
{
    using System;
    using System.Collections;
    using System.Globalization;
    using System.IO;
    using Reflector.CodeModel;
    using Reflector.CodeModel.Memory;

    internal class JavaScriptLanguage : ILanguage
    {
        private bool addInMode;

        public JavaScriptLanguage()
        {
            this.addInMode = false;
        }

        public JavaScriptLanguage(bool addInMode)
        {
            this.addInMode = addInMode;
        }

        public string Name
        {
            get
            {
                return (!this.addInMode) ? "JavaScript" : "JavaScript Add-In";
            }
        }

        public string FileExtension
        {
            get
            {
                return ".js";
            }
        }

        public bool Translate
        {
            get
            {
                return true;
            }
        }

        public ILanguageWriter GetWriter(IFormatter formatter, ILanguageWriterConfiguration configuration)
        {
            return new LanguageWriter(formatter, configuration);
        }

        internal class LanguageWriter : ILanguageWriter
        {
            private IFormatter formatter;
            private ILanguageWriterConfiguration configuration;

            private static Hashtable specialMethodNames;
            private static Hashtable specialTypeNames;
            private bool forLoop = false;
            private bool firstStmt = false;
            private int pendingOutdent = 0;
            private int blockStatementLevel = 0;
            private NumberFormat numberFormat;

            private enum NumberFormat
            {
                Auto,
                Hexadecimal,
                Decimal
            }

            public LanguageWriter(IFormatter formatter, ILanguageWriterConfiguration configuration)
            {
                this.formatter = formatter;
                this.configuration = configuration;

                if (specialTypeNames == null)
                {
                    specialTypeNames = new Hashtable();
                    specialTypeNames["Void"] = " ";
                    specialTypeNames["Object"] = "TObject";
                    specialTypeNames["String"] = "string";
                    specialTypeNames["SByte"] = "Shortint";
                    specialTypeNames["Byte"] = "Byte";
                    specialTypeNames["Int16"] = "Smallint";
                    specialTypeNames["UInt16"] = "Word";
                    specialTypeNames["Int32"] = "Integer";
                    specialTypeNames["UInt32"] = "Cardinal";
                    specialTypeNames["Int64"] = "Int64";
                    specialTypeNames["UInt64"] = "UInt64";
                    specialTypeNames["Char"] = "Char";
                    specialTypeNames["Boolean"] = "boolean";
                    specialTypeNames["Single"] = "Single";
                    specialTypeNames["Double"] = "Double";
                    specialTypeNames["Decimal"] = "Decimal";
                }

                if (specialMethodNames == null)
                {
                    specialMethodNames = new Hashtable();
                    specialMethodNames["op_UnaryPlus"] = "Positive";
                    specialMethodNames["op_Addition"] = "Add";
                    specialMethodNames["op_Increment"] = "Inc";
                    specialMethodNames["op_UnaryNegation"] = "Negative";
                    specialMethodNames["op_Subtraction"] = "Subtract";
                    specialMethodNames["op_Decrement"] = "Dec";
                    specialMethodNames["op_Multiply"] = "Multiply";
                    specialMethodNames["op_Division"] = "Divide";
                    specialMethodNames["op_Modulus"] = "Modulus";
                    specialMethodNames["op_BitwiseAnd"] = "BitwiseAnd";
                    specialMethodNames["op_BitwiseOr"] = "BitwiseOr";
                    specialMethodNames["op_ExclusiveOr"] = "BitwiseXor";
                    specialMethodNames["op_Negation"] = "LogicalNot";
                    specialMethodNames["op_OnesComplement"] = "BitwiseNot";
                    specialMethodNames["op_LeftShift"] = "ShiftLeft";
                    specialMethodNames["op_RightShift"] = "ShiftRight";
                    specialMethodNames["op_Equality"] = "Equal";
                    specialMethodNames["op_Inequality"] = "NotEqual";
                    specialMethodNames["op_GreaterThanOrEqual"] = "GreaterThanOrEqual";
                    specialMethodNames["op_LessThanOrEqual"] = "LessThanOrEqual";
                    specialMethodNames["op_GreaterThan"] = "GreaterThan";
                    specialMethodNames["op_LessThan"] = "LessThan";
                    specialMethodNames["op_True"] = "True";
                    specialMethodNames["op_False"] = "False";
                    specialMethodNames["op_Implicit"] = "Implicit";
                    specialMethodNames["op_Explicit"] = "Explicit";
                }

                switch (configuration["NumberFormat"])
                {
                    case "Hexadecimal":
                        this.numberFormat = NumberFormat.Hexadecimal;
                        break;

                    case "Decimal":
                        this.numberFormat = NumberFormat.Decimal;
                        break;

                    default:
                        this.numberFormat = NumberFormat.Auto;
                        break;
                }
            }

            public void WriteAssembly(IAssembly value)
            {
                this.formatter.Write("// JS Assembly");
                this.formatter.Write(" ");
                this.formatter.WriteDeclaration(value.Name);

                if (value.Version != null)
                {
                    this.formatter.Write(", ");
                    this.formatter.Write("Version");
                    this.formatter.Write(" ");
                    this.formatter.Write(value.Version.ToString());
                }

                this.formatter.WriteLine();

                if ((this.configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    this.formatter.WriteLine();
                    this.WriteCustomAttributeList(value, this.formatter);
                    this.formatter.WriteLine();
                }

                this.formatter.WriteProperty("Location", value.Location);
                this.formatter.WriteProperty("Name", value.ToString());

                switch (value.Type)
                {
                    case AssemblyType.Application:
                        this.formatter.WriteProperty("Type", "Windows Application");
                        break;

                    case AssemblyType.Console:
                        this.formatter.WriteProperty("Type", "Console Application");
                        break;

                    case AssemblyType.Library:
                        this.formatter.WriteProperty("Type", "Library");
                        break;
                }
            }

            public void WriteAssemblyReference(IAssemblyReference value)
            {
                this.formatter.Write("// Assembly Reference");
                this.formatter.Write(" ");
                this.formatter.WriteDeclaration(value.Name);
                this.formatter.WriteLine();

                this.formatter.WriteProperty("Version", value.Version.ToString());
                this.formatter.WriteProperty("Name", value.ToString());
            }

            public void WriteModule(IModule value)
            {
                this.formatter.Write("// Module");
                this.formatter.Write(" ");
                this.formatter.WriteDeclaration(value.Name);
                this.formatter.WriteLine();

                if ((this.configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    this.formatter.WriteLine();
                    this.WriteCustomAttributeList(value, this.formatter);
                    this.formatter.WriteLine();
                }

                this.formatter.WriteProperty("Version", value.Version.ToString());
                this.formatter.WriteProperty("Location", value.Location);

                string location = Environment.ExpandEnvironmentVariables(value.Location);
                if (File.Exists(location))
                {
                    this.formatter.WriteProperty("Size", new FileInfo(location).Length + " Bytes");
                }
            }

            public void WriteModuleReference(IModuleReference value)
            {
                this.formatter.Write("// Module Reference");
                this.formatter.Write(" ");
                this.formatter.WriteDeclaration(value.Name);
                this.formatter.WriteLine();
            }

            public void WriteResource(IResource value)
            {
                this.formatter.Write("// ");

                switch (value.Visibility)
                {
                    case ResourceVisibility.Public:
                        this.formatter.WriteKeyword("public");
                        break;

                    case ResourceVisibility.Private:
                        this.formatter.WriteKeyword("private");
                        break;
                }

                this.formatter.Write(" ");
                this.formatter.WriteKeyword("resource");
                this.formatter.Write(" ");
                this.formatter.WriteDeclaration(value.Name, value);
                this.formatter.WriteLine();

                IEmbeddedResource embeddedResource = value as IEmbeddedResource;
                if ((embeddedResource != null) && (embeddedResource.Value != null))
                {
                    this.formatter.WriteProperty("Size", embeddedResource.Value.Length.ToString(CultureInfo.InvariantCulture) + " bytes");
                }

                IFileResource fileResource = value as IFileResource;
                if (fileResource != null)
                {
                    this.formatter.WriteProperty("Location", fileResource.Location);
                }
            }

            public void WriteNamespace(INamespace value)
            {
                formatter.WriteKeyword("unit ");
                if (value.Name.Length != 0)
                {
                    formatter.Write(" ");
                    WriteDeclaration(value.Name, formatter);
                }

                formatter.Write(";");

                if (configuration["ShowNamespaceBody"] == "true")
                {
                    formatter.WriteLine();
                    formatter.WriteKeyword("interface");
                    formatter.WriteLine();
                    formatter.WriteKeyword("type");
                    formatter.WriteLine();
                    // formatter.WriteIndent();

                    ArrayList types = new ArrayList();
                    foreach (ITypeDeclaration typeDeclaration in value.Types)
                    {
                        if (Helper.IsVisible(typeDeclaration, configuration.Visibility))
                        {
                            types.Add(typeDeclaration);
                        }
                    }

                    types.Sort();

                    for (int i = 0; i < types.Count; i++)
                    {
                        if (i != 0)
                        {
                            formatter.WriteLine();
                        }

                        this.WriteTypeDeclaration((ITypeDeclaration)types[i]);
                    }

                    formatter.WriteOutdent();
                    formatter.WriteLine();
                    formatter.WriteLine();
                    formatter.WriteKeyword("implementation");
                    formatter.WriteLine();
                    formatter.WriteLine();
                    formatter.WriteComment("  ");
                    formatter.WriteComment("{...}");
                    formatter.WriteLine();
                    formatter.WriteLine();
                    formatter.WriteKeyword("end.");
                    formatter.WriteLine();
                    formatter.WriteLine();
                }
            }

            public void WriteTypeDeclaration(ITypeDeclaration value)
            {
                if ((configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    //this.WriteCustomAttributeList(value, formatter);
                    //formatter.WriteLine();
                }

                //WriteTypeVisibility(value.Visibility, formatter);

                formatter.Write(value.Namespace);
                formatter.Write(".");
                this.WriteDeclaration(value.Name, value, formatter);

                if (Helper.IsDelegate(value))
                {
                    IMethodDeclaration methodDeclaration = Helper.GetMethod(value, "Invoke");
                    string method = "procedure";
                    bool isFunction = false;
                    if (!IsType(methodDeclaration.ReturnType.Type, "System", "Void"))
                    {
                        method = "function";
                        isFunction = true;
                    }

                    formatter.WriteKeyword(method);
                    formatter.Write(" ");
                    WriteDeclaration(methodDeclaration.Name, value, formatter);

                    // Generic Parameters
                    this.WriteGenericArgumentList(methodDeclaration.GenericArguments, formatter);

                    // Method Parameters
                    if ((methodDeclaration.Parameters.Count > 0) || (methodDeclaration.CallingConvention == MethodCallingConvention.VariableArguments))
                    {
                        formatter.Write("(");
                        this.WriteParameterDeclarationList(methodDeclaration.Parameters, formatter, configuration);
                        formatter.Write(")");
                    }
                    this.WriteGenericParameterConstraintList(methodDeclaration, formatter);

                    if (isFunction)
                    {
                        formatter.Write(": ");
                        this.WriteType(methodDeclaration.ReturnType.Type, formatter);
                    }
                    formatter.Write(";");
                }
                else
                    if (Helper.IsEnumeration(value))
                    {
                        bool first = true;
                        formatter.Write("(");
                        foreach (IFieldDeclaration fieldDeclaration in Helper.GetFields(value, configuration.Visibility))
                        {
                            // Do not render underlying "value__" field
                            if ((!fieldDeclaration.SpecialName) || (!fieldDeclaration.RuntimeSpecialName) || (fieldDeclaration.FieldType.Equals(value)))
                            {
                                if (first)
                                {
                                    first = false;
                                }
                                else
                                {
                                    formatter.Write(", ");
                                }

                                this.WriteDeclaration(fieldDeclaration.Name, fieldDeclaration, formatter);
                                IExpression initializer = fieldDeclaration.Initializer;
                                if (initializer != null)
                                {
                                    formatter.Write("=");
                                    this.WriteExpression(initializer, formatter);
                                }
                            }
                        }
                        formatter.Write(");");
                    }
                    else
                    {
                        if (Helper.IsValueType(value))
                        {
                        }
                        else if (value.Interface)
                        {
                            //formatter.WriteKeyword("interface");
                            //this.WriteGenericArgumentList(value.GenericArguments, formatter);
                        }
                        else
                        {
                            formatter.Write(" = ");
                            formatter.WriteKeyword("function");
                            formatter.Write("() { };");
                            formatter.WriteLine();

                            if (value.Abstract)
                            {
                                //formatter.Write(" ");
                                //formatter.WriteKeyword("abstract");
                            }

                            if (value.Sealed)
                            {
                                //formatter.Write(" ");
                                //formatter.WriteKeyword("sealed");
                            }
                            //this.WriteGenericArgumentList(value.GenericArguments, formatter);

                            ITypeReference baseType = value.BaseType;
                            if ((baseType != null) && (!IsType(baseType, "System", "Object")))
                            {

                                formatter.Write(value.Namespace);
                                formatter.Write(".");
                                this.WriteDeclaration(value.Name, value, formatter);
                                formatter.Write(".");
                                formatter.WriteKeyword("prototype");
                                formatter.Write(" = ");
                                formatter.WriteKeyword("new");
                                formatter.Write(" ");
                                this.WriteType(baseType, formatter);
                                formatter.Write("();");
                            }
                        }

                        // TODO filter interfaces
                        foreach (ITypeReference interfaceType in value.Interfaces)
                        {
                            //formatter.Write(bracketPrinted ? ", " : " (");
                            //this.WriteType(interfaceType, formatter);
                            //bracketPrinted = true;
                        }

                        //this.WriteGenericParameterConstraintList(value, formatter);
                    }

                formatter.WriteProperty("Name", GetDelphiStyleResolutionScope(value));
                this.WriteDeclaringAssembly(Helper.GetAssemblyReference(value), formatter);

                if ((configuration["ShowTypeDeclarationBody"] == "true") && (!Helper.IsEnumeration(value)) && (!Helper.IsDelegate(value)))
                {
                    formatter.WriteLine();

                    bool newLine = false;
                    ICollection events = Helper.GetEvents(value, configuration.Visibility);
                    if (events.Count > 0)
                    {
                        if (newLine)
                            formatter.WriteLine();
                        newLine = true;
                        formatter.WriteComment("// Events");
                        formatter.WriteLine();

                        foreach (IEventDeclaration eventDeclaration in events)
                        {
                            this.WriteEventDeclaration(eventDeclaration);
                            formatter.WriteLine();
                        }
                    }

                    ICollection methods = Helper.GetMethods(value, configuration.Visibility);
                    if (methods.Count > 0)
                    {
                        if (newLine)
                            formatter.WriteLine();
                        newLine = true;
                        formatter.WriteComment("// Methods");
                        formatter.WriteLine();

                        foreach (IMethodDeclaration methodDeclaration in methods)
                        {
                            this.WriteMethodDeclaration(methodDeclaration);
                            formatter.WriteLine();
                        }
                    }

                    ICollection properties = Helper.GetProperties(value, configuration.Visibility);
                    if (properties.Count > 0)
                    {
                        if (newLine)
                            formatter.WriteLine();
                        newLine = true;
                        formatter.WriteComment("// Properties");
                        formatter.WriteLine();

                        foreach (IPropertyDeclaration propertyDeclaration in properties)
                        {
                            this.WritePropertyDeclaration(propertyDeclaration);
                            formatter.WriteLine();
                        }
                    }

                    ICollection fields = Helper.GetFields(value, configuration.Visibility);
                    if (fields.Count > 0)
                    {
                        if (newLine)
                            formatter.WriteLine();
                        newLine = true;
                        formatter.WriteComment("// Fields");
                        formatter.WriteLine();

                        foreach (IFieldDeclaration fieldDeclaration in fields)
                            if ((!fieldDeclaration.SpecialName) || (fieldDeclaration.Name != "value__"))
                            {
                                this.WriteFieldDeclaration(fieldDeclaration);
                                formatter.WriteLine();
                            }
                    }

                    ICollection nestedTypes = Helper.GetNestedTypes(value, configuration.Visibility);
                    if (nestedTypes.Count > 0)
                    {
                        if (newLine)
                            formatter.WriteLine();
                        newLine = true;

                        formatter.WriteKeyword("type");
                        formatter.Write(" ");
                        formatter.WriteComment("// Nested Types");
                        formatter.WriteLine();
                        formatter.WriteIndent();
                        foreach (ITypeDeclaration nestedTypeDeclaration in nestedTypes)
                        {
                            this.WriteTypeDeclaration(nestedTypeDeclaration);
                            formatter.WriteLine();
                        }
                        formatter.WriteOutdent();
                    }

                    formatter.WriteLine();
                    formatter.WriteOutdent();
                    formatter.WriteKeyword("end");
                    formatter.Write(";");
                    formatter.WriteLine();
                }
            }

            public void WriteTypeVisibility(TypeVisibility visibility, IFormatter formatter)
            {
                switch (visibility)
                {
                    case TypeVisibility.Public: formatter.WriteKeyword("public"); break;
                    case TypeVisibility.NestedPublic: formatter.WriteKeyword("public"); break;
                    case TypeVisibility.Private: formatter.WriteKeyword("strict private"); break;
                    case TypeVisibility.NestedAssembly: formatter.WriteKeyword("private"); break;
                    case TypeVisibility.NestedPrivate: formatter.WriteKeyword("strict private"); break;
                    case TypeVisibility.NestedFamily: formatter.WriteKeyword("strict protected"); break;
                    case TypeVisibility.NestedFamilyAndAssembly: formatter.WriteKeyword("protected"); break;
                    case TypeVisibility.NestedFamilyOrAssembly: formatter.WriteKeyword("protected");
                        formatter.Write(" ");
                        formatter.WriteComment("{internal}"); break;
                    default: throw new NotSupportedException();
                }
                formatter.Write(" ");
            }

            public void WriteFieldVisibility(FieldVisibility visibility, IFormatter formatter)
            {
                switch (visibility)
                {
                    case FieldVisibility.Public: formatter.WriteKeyword("public"); break;
                    case FieldVisibility.Private: formatter.WriteKeyword("strict private"); break;
                    case FieldVisibility.PrivateScope: formatter.WriteKeyword("private");
                        formatter.Write(" ");
                        formatter.WriteComment("{scope}"); break;
                    case FieldVisibility.Family: formatter.WriteKeyword("strict protected"); break;
                    case FieldVisibility.Assembly: formatter.WriteKeyword("private"); break;
                    case FieldVisibility.FamilyOrAssembly: formatter.WriteKeyword("protected"); break;
                    case FieldVisibility.FamilyAndAssembly: formatter.WriteKeyword("protected");
                        formatter.Write(" ");
                        formatter.WriteComment("{internal}"); break;
                    default: throw new NotSupportedException();
                }
                formatter.Write(" ");
            }

            public void WriteMethodVisibility(MethodVisibility visibility, IFormatter formatter)
            {
                switch (visibility)
                {
                    case MethodVisibility.Public: formatter.WriteKeyword("public"); break;
                    case MethodVisibility.Private: formatter.WriteKeyword("strict private"); break;
                    case MethodVisibility.PrivateScope: formatter.WriteKeyword("private");
                        formatter.Write(" ");
                        formatter.WriteComment("{scope}"); break;
                    case MethodVisibility.Family: formatter.WriteKeyword("strict protected"); break;
                    case MethodVisibility.Assembly: formatter.WriteKeyword("private"); break;
                    case MethodVisibility.FamilyOrAssembly: formatter.WriteKeyword("protected"); break;
                    case MethodVisibility.FamilyAndAssembly: formatter.WriteKeyword("protected");
                        formatter.Write(" ");
                        formatter.WriteComment("{internal}"); break;
                    default: throw new NotSupportedException();
                }
                formatter.Write(" ");
            }

            public void WriteFieldDeclaration(IFieldDeclaration value)
            {
                if ((configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    this.WriteCustomAttributeList(value, formatter);
                    formatter.WriteLine();
                }

                if (!this.IsEnumerationElement(value))
                {
                    WriteFieldVisibility(value.Visibility, formatter);
                    if ((value.Static) && (value.Literal))
                    {
                        formatter.WriteKeyword("const");
                        formatter.Write(" ");
                    }
                    else
                    {
                        if (value.Static)
                        {
                            formatter.WriteKeyword("class var");
                            formatter.Write(" ");
                        }
                        if (value.ReadOnly)
                        {
                            formatter.WriteKeyword("{readonly}");
                            formatter.Write(" ");
                        }
                    }

                    this.WriteDeclaration(value.Name, value, formatter);
                    formatter.Write(": ");
                    this.WriteType(value.FieldType, formatter);
                }
                else
                {
                    this.WriteDeclaration(value.Name, value, formatter);
                }

                byte[] data = null;

                IExpression initializer = value.Initializer;
                if (initializer != null)
                {
                    ILiteralExpression literalExpression = initializer as ILiteralExpression;
                    if ((literalExpression != null) && (literalExpression.Value != null) && (literalExpression.Value is byte[]))
                    {
                        data = (byte[])literalExpression.Value;
                    }
                    else
                    {
                        formatter.Write(" = ");
                        this.WriteExpression(initializer, formatter);
                    }
                }

                if (!this.IsEnumerationElement(value))
                {
                    formatter.Write(";");
                }

                if (data != null)
                {
                    this.formatter.WriteComment(" // data size: " + data.Length.ToString(CultureInfo.InvariantCulture) + " bytes");
                }

                this.WriteDeclaringType(value.DeclaringType as ITypeReference, formatter);
            }

            public void WriteMethodDeclaration(IMethodDeclaration value)
            {
                if (value.Body == null)
                {
                    if ((configuration["ShowCustomAttributes"] == "true") && (value.ReturnType.Attributes.Count != 0))
                    {
                        this.WriteCustomAttributeList(value.ReturnType, formatter);
                        formatter.WriteLine();
                    }

                    if ((configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                    {
                        this.WriteCustomAttributeList(value, formatter);
                        formatter.WriteLine();
                    }

                    this.WriteMethodAttributes(value, formatter);

                    if (this.GetCustomAttribute(value, "System.Runtime.InteropServices", "DllImportAttribute") != null)
                    {
                        formatter.WriteKeyword("extern");
                        formatter.Write(" ");
                    }
                }

                string methodName = value.Name;

                if (this.IsConstructor(value))
                {
                    methodName = "Create";
                }
                else
                    if ((value.SpecialName) && (specialMethodNames.Contains(methodName)))
                    {
                    }
                    else
                    {
                        if (!IsType(value.ReturnType.Type, "System", "Void"))
                        {
                        }
                    }

                if (value.Body != null)
                {
                    this.WriteDeclaringTypeReference(value.DeclaringType as ITypeReference, formatter);
                    formatter.Write("prototype.");
                }

                this.WriteDeclaration(methodName, value, formatter);

                formatter.Write(" = ");
                formatter.WriteKeyword("function");

                // Generic Parameters
                //this.WriteGenericArgumentList(value.GenericArguments, formatter);

                // Method Parameters
                formatter.Write("(");
                this.WriteParameterDeclarationList(value.Parameters, formatter, configuration);
                //if (value.CallingConvention == MethodCallingConvention.VariableArguments)
                //{
                //	formatter.Write(" {; __arglist}");
                //}
                formatter.Write(")");

                //this.WriteGenericParameterConstraintList(value, formatter);

                formatter.Write(" {");
                formatter.WriteLine();
                formatter.WriteIndent();

                IBlockStatement body = value.Body as IBlockStatement;
                if (body == null)
                {
                    //this.WriteMethodDirectives(value, formatter);
                }
                else
                {
                    // Method Body

                    // we need to dump the Delphi Variable list first
                    bool hasvar = false;
                    this.WriteVariableList(body.Statements, formatter, ref hasvar);
                    blockStatementLevel = 0; // to optimize exit() for Delphi

                    this.WriteStatement(body, formatter);
                    this.WritePendingOutdent(formatter);

                    formatter.WriteLine();
                }
                formatter.WriteOutdent();
                formatter.Write("}");
                formatter.WriteLine();

                //this.WriteDeclaringType(value.DeclaringType as ITypeReference, formatter);			
            }

            public void WritePropertyDeclaration(IPropertyDeclaration value)
            {
                if ((configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    this.WriteCustomAttributeList(value, formatter);
                    formatter.WriteLine();
                }

                IMethodDeclaration getMethod = null;
                if (value.GetMethod != null)
                {
                    getMethod = value.GetMethod.Resolve();
                }

                IMethodDeclaration setMethod = null;
                if (value.SetMethod != null)
                {
                    setMethod = value.SetMethod.Resolve();
                }

                bool hasSameAttributes = true;
                if ((getMethod != null) && (setMethod != null))
                {
                    hasSameAttributes &= (getMethod.Visibility == setMethod.Visibility);
                    hasSameAttributes &= (getMethod.Static == setMethod.Static);
                    hasSameAttributes &= (getMethod.Final == setMethod.Final);
                    hasSameAttributes &= (getMethod.Virtual == setMethod.Virtual);
                    hasSameAttributes &= (getMethod.Abstract == setMethod.Abstract);
                    hasSameAttributes &= (getMethod.NewSlot == setMethod.NewSlot);
                }

                if (hasSameAttributes)
                {
                    if (getMethod != null)
                    {
                        this.WriteMethodAttributes(getMethod, formatter);
                    }
                    else if (setMethod != null)
                    {
                        this.WriteMethodAttributes(setMethod, formatter);
                    }
                }

                formatter.WriteKeyword("property");
                formatter.Write(" ");

                // Name
                string propertyName = value.Name;
                //if (propertyName == "Item")
                //	propertyName = "Item";

                this.WriteDeclaration(propertyName, value, formatter);

                IParameterDeclarationCollection parameters = value.Parameters;
                if (parameters.Count > 0)
                {
                    formatter.Write("(");
                    this.WriteParameterDeclarationList(parameters, formatter, configuration);
                    formatter.Write(")");
                }
                formatter.Write(": ");

                // PropertyType
                this.WriteType(value.PropertyType, formatter);

                if (getMethod != null)
                {
                    formatter.Write(" ");
                    if (!hasSameAttributes)
                    {
                        formatter.Write("{");
                        this.WriteMethodAttributes(getMethod, formatter);
                        formatter.Write("}");
                        formatter.Write(" ");
                    }

                    formatter.WriteKeyword("read");
                    formatter.Write(" ");
                    WriteMethodReference(getMethod, formatter);
                }

                if (setMethod != null)
                {
                    formatter.Write(" ");
                    if (!hasSameAttributes)
                    {
                        formatter.Write("{");
                        this.WriteMethodAttributes(setMethod, formatter);
                        formatter.Write("}");
                        formatter.Write(" ");
                    }

                    formatter.WriteKeyword("write");
                    formatter.Write(" ");
                    WriteMethodReference(setMethod, formatter);
                }

                if (value.Initializer != null)
                { // in Delphi we do not have a property initializer. Or do we ?
                    // PS
                    formatter.Write("{(pseudo) := ");
                    this.WriteExpression(value.Initializer, formatter);
                    formatter.Write(" }");
                }


                formatter.Write(";");
                this.WriteDeclaringType(value.DeclaringType as ITypeReference, formatter);
            }

            public void WriteEventDeclaration(IEventDeclaration value)
            {
                if ((configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    this.WriteCustomAttributeList(value, formatter);
                    formatter.WriteLine();
                }

                ITypeDeclaration declaringType = (value.DeclaringType as ITypeReference).Resolve();
                if (!declaringType.Interface)
                {
                    WriteMethodVisibility(Helper.GetVisibility(value), formatter);
                }

                if (Helper.IsStatic(value))
                {
                    formatter.WriteKeyword("static");
                    formatter.Write(" ");
                }

                formatter.Write("event");
                formatter.Write(" ");
                this.WriteType(value.EventType, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword(value.Name);
                formatter.Write(";");
                this.WriteDeclaringType(value.DeclaringType as ITypeReference, formatter);

            }

            private void WriteDeclaringTypeReference(ITypeReference value, IFormatter formatter)
            {
                ITypeReference owner = (value.Owner as ITypeReference);
                if (owner != null)
                {
                    WriteDeclaringTypeReference(owner, formatter);
                }
                this.WriteType(value, formatter);
                formatter.Write(".");
            }

            private string GetDelphiStyleResolutionScope(ITypeReference reference)
            {
                string result = reference.ToString();
                while (true)
                {
                    ITypeReference OwnerRef = (reference.Owner as ITypeReference);
                    if (OwnerRef == null)
                    {
                        string namespacestr = reference.Namespace;
                        if (namespacestr.Length == 0)
                            return result;
                        else
                            return namespacestr + "." + result;
                    }
                    reference = OwnerRef;
                    result = reference.ToString() + "." + result;
                }
            }




            private void WriteType(IType type, IFormatter formatter)
            {
                ITypeReference typeReference = type as ITypeReference;
                if (typeReference != null)
                {
                    string description = Helper.GetNameWithResolutionScope(typeReference);
                    this.WriteTypeReference(typeReference, formatter, description, typeReference);
                    return;
                }

                IArrayType arrayType = type as IArrayType;
                if (arrayType != null)
                {
                    this.WriteType(arrayType.ElementType, formatter);
                    formatter.Write("[");

                    IArrayDimensionCollection dimensions = arrayType.Dimensions;
                    for (int i = 0; i < dimensions.Count; i++)
                    {
                        if (i != 0)
                        {
                            formatter.Write(",");
                        }

                        if ((dimensions[i].LowerBound != 0) && (dimensions[i].UpperBound != -1))
                        {
                            if ((dimensions[i].LowerBound != -1) || (dimensions[i].UpperBound != -1))
                            {
                                formatter.Write((dimensions[i].LowerBound != -1) ? dimensions[i].LowerBound.ToString(CultureInfo.InvariantCulture) : ".");
                                formatter.Write("..");
                                formatter.Write((dimensions[i].UpperBound != -1) ? dimensions[i].UpperBound.ToString(CultureInfo.InvariantCulture) : ".");
                            }
                        }
                    }

                    formatter.Write("]");
                    return;
                }

                IPointerType pointerType = type as IPointerType;
                if (pointerType != null)
                {
                    this.WriteType(pointerType.ElementType, formatter);
                    formatter.Write("*");
                    return;
                }

                IReferenceType referenceType = type as IReferenceType;
                if (referenceType != null)
                {
                    // formatter.WriteKeyword ("var"); // already done before the param name - HV
                    // formatter.Write (" ");
                    this.WriteType(referenceType.ElementType, formatter);
                    return;
                }

                IOptionalModifier optionalModifier = type as IOptionalModifier;
                if (optionalModifier != null)
                {
                    this.WriteType(optionalModifier.ElementType, formatter);
                    formatter.Write(" ");
                    formatter.WriteKeyword("modopt");
                    formatter.Write("(");
                    this.WriteType(optionalModifier.Modifier, formatter);
                    formatter.Write(")");
                    return;
                }

                IRequiredModifier requiredModifier = type as IRequiredModifier;
                if (requiredModifier != null)
                {
                    this.WriteType(requiredModifier.ElementType, formatter);
                    formatter.Write(" ");
                    formatter.WriteKeyword("modreq");
                    formatter.Write("(");
                    this.WriteType(requiredModifier.Modifier, formatter);
                    formatter.Write(")");
                    return;
                }

                IFunctionPointer functionPointer = type as IFunctionPointer;
                if (functionPointer != null)
                {
                    this.WriteType(functionPointer.ReturnType.Type, formatter);
                    formatter.Write(" *(");
                    for (int i = 0; i < functionPointer.Parameters.Count; i++)
                    {
                        if (i != 0)
                        {
                            formatter.Write(", ");
                        }

                        this.WriteType(functionPointer.Parameters[i].ParameterType, formatter);
                    }

                    formatter.Write(")");
                    return;
                }

                IGenericParameter genericParameter = type as IGenericParameter;
                if (genericParameter != null)
                {
                    formatter.Write(genericParameter.Name);
                    return;
                }

                IGenericArgument genericArgument = type as IGenericArgument;
                if (genericArgument != null)
                {
                    this.WriteType(genericArgument.Resolve(), formatter);
                    return;
                }

                throw new NotSupportedException();
            }

            private void WriteMethodAttributes(IMethodDeclaration methodDeclaration, IFormatter formatter)
            {
                ITypeDeclaration declaringType = (methodDeclaration.DeclaringType as ITypeReference).Resolve();
                if (!declaringType.Interface)
                {
                    WriteMethodVisibility(methodDeclaration.Visibility, formatter);

                    if (methodDeclaration.Static)
                    {
                        formatter.WriteKeyword("class");
                        formatter.Write(" ");
                    }
                }
            }

            private void WriteMethodDirectives(IMethodDeclaration methodDeclaration, IFormatter formatter)
            {
                ITypeDeclaration declaringType = (methodDeclaration.DeclaringType as ITypeReference).Resolve();
                if (!declaringType.Interface)
                {
                    formatter.Write(" ");

                    if (methodDeclaration.Static)
                    {
                        formatter.Write("static;");
                        formatter.Write(" ");
                    }

                    if ((methodDeclaration.Final) && (!methodDeclaration.NewSlot))
                    {
                        formatter.WriteKeyword("final;");
                        formatter.Write(" ");
                    }

                    if (methodDeclaration.Virtual)
                    {
                        if (methodDeclaration.Abstract)
                        {
                            formatter.WriteKeyword("abstract;");
                            formatter.Write(" ");
                        }
                        else if ((methodDeclaration.NewSlot) && (!methodDeclaration.Final))
                        {
                            formatter.WriteKeyword("virtual;");
                            formatter.Write(" ");
                        }

                        if (!methodDeclaration.NewSlot)
                        {
                            formatter.WriteKeyword("override;");
                            formatter.Write(" ");
                        }
                    }
                }
            }

            private void WriteParameterDeclaration(IParameterDeclaration value, IFormatter formatter, ILanguageWriterConfiguration configuration)
            {
                if ((configuration != null) && (configuration["ShowCustomAttributes"] == "true") && (value.Attributes.Count != 0))
                {
                    this.WriteCustomAttributeList(value, formatter);
                    formatter.Write(" ");
                }

                IType parameterType = value.ParameterType;

                IReferenceType referenceType = parameterType as IReferenceType;
                if (referenceType != null)
                {
                }

                if ((value.Name != null) && value.Name.Length > 0)
                {
                    formatter.Write(value.Name);
                }
                else
                {
                    formatter.Write("A");
                }
            }

            private void WriteParameterDeclarationList(IParameterDeclarationCollection parameters, IFormatter formatter, ILanguageWriterConfiguration configuration)
            {
                for (int i = 0; i < parameters.Count; i++)
                {
                    IParameterDeclaration parameter = parameters[i];
                    IType parameterType = parameter.ParameterType;
                    if ((parameterType != null) || ((i + 1) != parameters.Count))
                    {
                        if (i != 0)
                        {
                            formatter.Write(", ");
                        }

                        this.WriteParameterDeclaration(parameter, formatter, configuration);
                    }
                }
            }

            private void WriteCustomAttribute(ICustomAttribute customAttribute, IFormatter formatter)
            {
                ITypeReference type = (customAttribute.Constructor.DeclaringType as ITypeReference);
                string name = type.Name;

                if (name.EndsWith("Attribute"))
                {
                    name = name.Substring(0, name.Length - 9);
                }

                this.WriteReference(name, formatter, this.GetMethodReferenceDescription(customAttribute.Constructor), customAttribute.Constructor);

                IExpressionCollection expression = customAttribute.Arguments;
                if (expression.Count != 0)
                {
                    formatter.Write("(");
                    for (int i = 0; i < expression.Count; i++)
                    {
                        if (i != 0)
                        {
                            formatter.Write(", ");
                        }

                        this.WriteExpression(expression[i], formatter);
                    }

                    formatter.Write(")");
                }
            }

            private void WriteCustomAttributeList(ICustomAttributeProvider provider, IFormatter formatter)
            {
                ArrayList attributes = new ArrayList();
                for (int i = 0; i < provider.Attributes.Count; i++)
                {
                    ICustomAttribute attribute = provider.Attributes[i];
                    if (IsType(attribute.Constructor.DeclaringType, "System.Runtime.InteropServices", "DefaultParameterValueAttribute", "System"))
                    {
                        continue;
                    }

                    attributes.Add(attribute);
                }

                if (attributes.Count > 0)
                {
                    string prefix = null;

                    IAssembly assembly = provider as IAssembly;
                    if (assembly != null)
                    {
                        prefix = "assembly:";
                    }

                    IModule module = provider as IModule;
                    if (module != null)
                    {
                        prefix = "module:";
                    }

                    IMethodReturnType methodReturnType = provider as IMethodReturnType;
                    if (methodReturnType != null)
                    {
                        prefix = "return:";
                    }

                    if ((assembly != null) || (module != null))
                    {
                        for (int i = 0; i < attributes.Count; i++)
                        {
                            ICustomAttribute attribute = (ICustomAttribute)attributes[i];
                            formatter.Write("[");
                            formatter.WriteKeyword(prefix);
                            formatter.Write(" ");
                            this.WriteCustomAttribute(attribute, formatter);
                            formatter.Write("]");

                            if (i != (attributes.Count - 1))
                            {
                                formatter.WriteLine();
                            }
                        }
                    }
                    else
                    {
                        formatter.Write("[");
                        if (prefix != null)
                        {
                            formatter.WriteKeyword(prefix);
                            formatter.Write(" ");
                        }

                        for (int i = 0; i < attributes.Count; i++)
                        {
                            if (i != 0)
                            {
                                formatter.Write(", ");
                            }

                            ICustomAttribute attribute = (ICustomAttribute)attributes[i];
                            this.WriteCustomAttribute(attribute, formatter);
                        }

                        formatter.Write("]");
                    }
                }
            }

            private void WriteGenericArgumentList(ITypeCollection parameters, IFormatter formatter)
            {
                if (parameters.Count > 0)
                {
                    formatter.Write("<");
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        if (i != 0)
                        {
                            formatter.Write("; ");
                        }

                        this.WriteType(parameters[i], formatter);
                    }

                    formatter.Write(">");
                }
            }

            private void WriteGenericParameterConstraint(IType value, IFormatter formatter)
            {
                IDefaultConstructorConstraint defaultConstructorConstraint = value as IDefaultConstructorConstraint;
                if (defaultConstructorConstraint != null)
                {
                    formatter.WriteKeyword("new");
                    formatter.Write("()");
                    return;
                }

                IReferenceTypeConstraint referenceTypeConstraint = value as IReferenceTypeConstraint;
                if (referenceTypeConstraint != null)
                {
                    formatter.WriteKeyword("class");
                    return;
                }

                IValueTypeConstraint valueTypeConstraint = value as IValueTypeConstraint;
                if (valueTypeConstraint != null)
                {
                    formatter.WriteKeyword("struct");
                    return;
                }

                this.WriteType(value, formatter);
            }

            private void WriteGenericParameterConstraintList(IGenericArgumentProvider provider, IFormatter formatter)
            {
                ITypeCollection genericArguments = provider.GenericArguments;
                if (genericArguments.Count > 0)
                {
                    for (int i = 0; i < genericArguments.Count; i++)
                    {
                        IGenericParameter parameter = genericArguments[i] as IGenericParameter;
                        if ((parameter != null) && (parameter.Constraints.Count > 0))
                        {
                            formatter.Write(" ");
                            formatter.WriteKeyword("where");
                            formatter.Write(" ");
                            formatter.Write(parameter.Name);
                            formatter.Write(":");
                            formatter.Write(" ");

                            for (int j = 0; j < parameter.Constraints.Count; j++)
                            {
                                if (j != 0)
                                {
                                    formatter.Write(", ");
                                }

                                IType constraint = (IType)parameter.Constraints[j];
                                this.WriteGenericParameterConstraint(constraint, formatter);
                            }
                        }
                    }
                }
            }

            #region Expression

            public void WriteExpression(IExpression value)
            {
                this.WriteExpression(value, formatter);
            }

            private void WriteExpression(IExpression value, IFormatter formatter)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                if (value is ILiteralExpression)
                {
                    this.WriteLiteralExpression(value as ILiteralExpression, formatter);
                    return;
                }

                if (value is IAssignExpression)
                {
                    this.WriteAssignExpression(value as IAssignExpression, formatter);
                    return;
                }

                if (value is ITypeOfExpression)
                {
                    this.WriteTypeOfExpression(value as ITypeOfExpression, formatter);
                    return;
                }

                if (value is IFieldOfExpression)
                {
                    this.WriteFieldOfExpression(value as IFieldOfExpression, formatter);
                    return;
                }

                if (value is IMethodOfExpression)
                {
                    this.WriteMethodOfExpression(value as IMethodOfExpression, formatter);
                    return;
                }

                if (value is IMemberInitializerExpression)
                {
                    this.WriteMemberInitializerExpression(value as IMemberInitializerExpression, formatter);
                    return;
                }

                if (value is ITypeReferenceExpression)
                {
                    this.WriteTypeReferenceExpression(value as ITypeReferenceExpression, formatter);
                    return;
                }

                if (value is IFieldReferenceExpression)
                {
                    this.WriteFieldReferenceExpression(value as IFieldReferenceExpression, formatter);
                    return;
                }

                if (value is IEventReferenceExpression)
                {
                    this.WriteEventReferenceExpression(value as IEventReferenceExpression, formatter);
                    return;
                }

                if (value is IMethodReferenceExpression)
                {
                    this.WriteMethodReferenceExpression(value as IMethodReferenceExpression, formatter);
                    return;
                }

                if (value is IArgumentListExpression)
                {
                    this.WriteArgumentListExpression(value as IArgumentListExpression, formatter);
                    return;
                }

                if (value is IStackAllocateExpression)
                {
                    this.WriteStackAllocateExpression(value as IStackAllocateExpression, formatter);
                    return;
                }

                if (value is IPropertyReferenceExpression)
                {
                    this.WritePropertyReferenceExpression(value as IPropertyReferenceExpression, formatter);
                    return;
                }

                if (value is IArrayCreateExpression)
                {
                    this.WriteArrayCreateExpression(value as IArrayCreateExpression, formatter);
                    return;
                }

                if (value is IBlockExpression)
                {
                    this.WriteBlockExpression(value as IBlockExpression, formatter);
                    return;
                }

                if (value is IBaseReferenceExpression)
                {
                    this.WriteBaseReferenceExpression(value as IBaseReferenceExpression, formatter);
                    return;
                }

                if (value is IUnaryExpression)
                {
                    this.WriteUnaryExpression(value as IUnaryExpression, formatter);
                    return;
                }

                if (value is IBinaryExpression)
                {
                    this.WriteBinaryExpression(value as IBinaryExpression, formatter);
                    return;
                }

                if (value is ITryCastExpression)
                {
                    this.WriteTryCastExpression(value as ITryCastExpression, formatter);
                    return;
                }

                if (value is ICanCastExpression)
                {
                    this.WriteCanCastExpression(value as ICanCastExpression, formatter);
                    return;
                }

                if (value is ICastExpression)
                {
                    this.WriteCastExpression(value as ICastExpression, formatter);
                    return;
                }

                if (value is IConditionExpression)
                {
                    this.WriteConditionExpression(value as IConditionExpression, formatter);
                    return;
                }

                if (value is INullCoalescingExpression)
                {
                    this.WriteNullCoalescingExpression(value as INullCoalescingExpression, formatter);
                    return;
                }

                if (value is IDelegateCreateExpression)
                {
                    this.WriteDelegateCreateExpression(value as IDelegateCreateExpression, formatter);
                    return;
                }

                if (value is IAnonymousMethodExpression)
                {
                    this.WriteAnonymousMethodExpression(value as IAnonymousMethodExpression, formatter);
                    return;
                }

                if (value is IArgumentReferenceExpression)
                {
                    this.WriteArgumentReferenceExpression(value as IArgumentReferenceExpression, formatter);
                    return;
                }

                if (value is IVariableDeclarationExpression)
                {
                    this.WriteVariableDeclarationExpression(value as IVariableDeclarationExpression, formatter);
                    return;
                }

                if (value is IVariableReferenceExpression)
                {
                    this.WriteVariableReferenceExpression(value as IVariableReferenceExpression, formatter);
                    return;
                }

                if (value is IPropertyIndexerExpression)
                {
                    this.WritePropertyIndexerExpression(value as IPropertyIndexerExpression, formatter);
                    return;
                }

                if (value is IArrayIndexerExpression)
                {
                    this.WriteArrayIndexerExpression(value as IArrayIndexerExpression, formatter);
                    return;
                }

                if (value is IMethodInvokeExpression)
                {
                    this.WriteMethodInvokeExpression(value as IMethodInvokeExpression, formatter);
                    return;
                }

                if (value is IDelegateInvokeExpression)
                {
                    this.WriteDelegateInvokeExpression(value as IDelegateInvokeExpression, formatter);
                    return;
                }

                if (value is IObjectCreateExpression)
                {
                    this.WriteObjectCreateExpression(value as IObjectCreateExpression, formatter);
                    return;
                }

                if (value is IThisReferenceExpression)
                {
                    this.WriteThisReferenceExpression(value as IThisReferenceExpression, formatter);
                    return;
                }

                if (value is IAddressOfExpression)
                {
                    this.WriteAddressOfExpression(value as IAddressOfExpression, formatter);
                    return;
                }

                if (value is IAddressReferenceExpression)
                {
                    this.WriteAddressReferenceExpression(value as IAddressReferenceExpression, formatter);
                    return;
                }

                if (value is IAddressOutExpression)
                {
                    this.WriteAddressOutExpression(value as IAddressOutExpression, formatter);
                    return;
                }

                if (value is IAddressDereferenceExpression)
                {
                    this.WriteAddressDereferenceExpression(value as IAddressDereferenceExpression, formatter);
                    return;
                }

                if (value is ISizeOfExpression)
                {
                    this.WriteSizeOfExpression(value as ISizeOfExpression, formatter);
                    return;
                }

                if (value is ITypeOfTypedReferenceExpression)
                {
                    this.WriteTypeOfTypedReferenceExpression(value as ITypeOfTypedReferenceExpression, formatter);
                    return;
                }

                if (value is IValueOfTypedReferenceExpression)
                {
                    this.WriteValueOfTypedReferenceExpression(value as IValueOfTypedReferenceExpression, formatter);
                    return;
                }

                if (value is ITypedReferenceCreateExpression)
                {
                    this.WriteTypedReferenceCreateExpression(value as ITypedReferenceCreateExpression, formatter);
                    return;
                }

                if (value is IGenericDefaultExpression)
                {
                    this.WriteGenericDefaultExpression(value as IGenericDefaultExpression, formatter);
                    return;
                }

                if (value is IQueryExpression)
                {
                    this.WriteQueryExpression(value as IQueryExpression, formatter);
                    return;
                }

                if (value is ILambdaExpression)
                {
                    this.WriteLambdaExpression(value as ILambdaExpression, formatter);
                    return;
                }

                if (value is ISnippetExpression)
                {
                    this.WriteSnippetExpression(value as ISnippetExpression, formatter);
                    return;
                }

                throw new ArgumentException("Invalid expression type.", "value");
            }

            private void WriteExpressionList(IExpressionCollection expressions, IFormatter formatter)
            {
                // Indent++;
                for (int i = 0; i < expressions.Count; i++)
                {
                    if (i != 0)
                    {
                        formatter.Write(", ");
                    }

                    this.WriteExpression(expressions[i], formatter);
                }
                // Indent--;
            }

            private void WriteGenericDefaultExpression(IGenericDefaultExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("default");
                formatter.Write("(");
                this.WriteType(value.GenericArgument, formatter);
                formatter.Write(")");
            }

            private void WriteTypeOfTypedReferenceExpression(ITypeOfTypedReferenceExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("__reftype");
                formatter.Write("(");
                this.WriteExpression(value.Expression, formatter);
                formatter.Write(")");
            }

            private void WriteValueOfTypedReferenceExpression(IValueOfTypedReferenceExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("__refvalue");
                formatter.Write("(");
                this.WriteExpression(value.Expression, formatter);
                formatter.Write(")");
            }

            private void WriteTypedReferenceCreateExpression(ITypedReferenceCreateExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("__makeref");
                formatter.Write("(");
                this.WriteExpression(value.Expression, formatter);
                formatter.Write(")");
            }

            private void WriteMemberInitializerExpression(IMemberInitializerExpression value, IFormatter formatter)
            {
                this.WriteMemberReference(value.Member, formatter);
                formatter.Write("=");
                this.WriteExpression(value.Value, formatter);
            }

            private void WriteMemberReference(IMemberReference memberReference, IFormatter formatter)
            {
                IFieldReference fieldReference = memberReference as IFieldReference;
                if (fieldReference != null)
                {
                    this.WriteFieldReference(fieldReference, formatter);
                }

                IMethodReference methodReference = memberReference as IMethodReference;
                if (methodReference != null)
                {
                    this.WriteMethodReference(methodReference, formatter);
                }

                IPropertyReference propertyReference = memberReference as IPropertyReference;
                if (propertyReference != null)
                {
                    this.WritePropertyReference(propertyReference, formatter);
                }

                IEventReference eventReference = memberReference as IEventReference;
                if (eventReference != null)
                {
                    this.WriteEventReference(eventReference, formatter);
                }
            }

            private void WriteTargetExpression(IExpression expression, IFormatter formatter)
            {
                this.WriteExpression(expression, formatter);
            }

            private void WriteTypeOfExpression(ITypeOfExpression expression, IFormatter formatter)
            {
                formatter.WriteKeyword("typeof");
                formatter.Write("(");
                this.WriteType(expression.Type, formatter);
                formatter.Write(")");
            }

            private void WriteFieldOfExpression(IFieldOfExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("fieldof");
                formatter.Write("(");
                this.WriteType(value.Field.DeclaringType, formatter);
                formatter.Write(".");
                formatter.WriteReference(value.Field.Name, this.GetFieldReferenceDescription(value.Field), value.Field);

                if (value.Type != null)
                {
                    formatter.Write(", ");
                    this.WriteType(value.Type, formatter);
                }

                formatter.Write(")");
            }

            private void WriteMethodOfExpression(IMethodOfExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("methodof");
                formatter.Write("(");

                this.WriteType(value.Method.DeclaringType, formatter);
                formatter.Write(".");
                formatter.WriteReference(value.Method.Name, this.GetMethodReferenceDescription(value.Method), value.Method);

                if (value.Type != null)
                {
                    formatter.Write(", ");
                    this.WriteType(value.Type, formatter);
                }

                formatter.Write(")");
            }

            private void WriteArrayElementType(IType type, IFormatter formatter)
            {
                IArrayType arrayType = type as IArrayType;
                if (arrayType != null)
                {
                    this.WriteArrayElementType(arrayType.ElementType, formatter);
                }
                else
                {
                    this.WriteType(type, formatter);
                }
            }

            private void WriteArrayCreateExpression(IArrayCreateExpression expression, IFormatter formatter)
            {
                if (expression.Initializer != null)
                {
                    this.WriteExpression(expression.Initializer, formatter);
                }
                else
                {
                    if (expression.Dimensions.Count == 1 && (expression.Dimensions[0] is ILiteralExpression) && ((ILiteralExpression)expression.Dimensions[0]).Value.Equals(0))
                    {
                        formatter.Write("[]");
                    }
                    else
                    {
                        formatter.Write("Array");
                        formatter.Write("(");
                        this.WriteExpressionList(expression.Dimensions, formatter);
                        formatter.Write(")");
                    }
                }
            }

            private void WriteBlockExpression(IBlockExpression expression, IFormatter formatter)
            {
                formatter.Write("[");

                if (expression.Expressions.Count > 16)
                {
                    formatter.WriteLine();
                    formatter.WriteIndent();
                }

                for (int i = 0; i < expression.Expressions.Count; i++)
                {
                    if (i != 0)
                    {
                        formatter.Write(", ");

                        if ((i % 16) == 0)
                        {
                            formatter.WriteLine();
                        }
                    }

                    this.WriteExpression(expression.Expressions[i], formatter);
                }

                if (expression.Expressions.Count > 16)
                {
                    formatter.WriteOutdent();
                    formatter.WriteLine();
                }

                formatter.Write("]");
            }

            private void WriteBaseReferenceExpression(IBaseReferenceExpression expression, IFormatter formatter)
            {
                formatter.WriteKeyword("this");
            }

            private void WriteTryCastExpression(ITryCastExpression expression, IFormatter formatter)
            {
                formatter.Write("(");
                this.WriteExpression(expression.Expression, formatter);
                formatter.WriteKeyword(" as ");
                this.WriteType(expression.TargetType, formatter);
                formatter.Write(")");
            }

            private void WriteCanCastExpression(ICanCastExpression expression, IFormatter formatter)
            {
                formatter.Write("(");
                this.WriteExpression(expression.Expression, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("is");
                formatter.Write(" ");
                this.WriteType(expression.TargetType, formatter);
                formatter.Write(")");
            }

            private void WriteCastExpression(ICastExpression expression, IFormatter formatter)
            {
                //formatter.Write("(");
                this.WriteExpression(expression.Expression, formatter);
                //formatter.Write(" ");
                //formatter.WriteKeyword("as");
                //formatter.Write(" ");
                //this.WriteType(expression.TargetType, formatter);
                //formatter.Write(")");
            }

            private void WriteConditionExpression(IConditionExpression expression, IFormatter formatter)
            {
                formatter.Write("(");
                this.WriteExpression(expression.Condition, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("?");
                formatter.Write(" ");
                this.WriteExpression(expression.Then, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword(":");
                formatter.Write(" ");
                this.WriteExpression(expression.Else, formatter);
                formatter.Write(")");
            }

            private void WriteNullCoalescingExpression(INullCoalescingExpression value, IFormatter formatter)
            {
                formatter.Write("(");
                this.WriteExpression(value.Condition, formatter);
                formatter.Write("!!");
                formatter.Write(" ");
                formatter.WriteKeyword("?");
                formatter.Write(" ");
                this.WriteExpression(value.Condition, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword(":");
                formatter.Write(" ");
                this.WriteExpression(value.Expression, formatter);
                formatter.Write(")");
            }


            private void WriteDelegateCreateExpression(IDelegateCreateExpression expression, IFormatter formatter)
            {
                this.WriteTypeReference(expression.DelegateType, formatter);
                formatter.Write(".");
                formatter.Write("Create");
                formatter.Write("(");
                this.WriteTargetExpression(expression.Target, formatter);
                formatter.Write(",");
                this.WriteMethodReference(expression.Method, formatter); // TODO Escape = true
                formatter.Write(")");
            }

            private void WriteAnonymousMethodExpression(IAnonymousMethodExpression value, IFormatter formatter)
            {
                bool parameters = false;

                for (int i = 0; i < value.Parameters.Count; i++)
                {
                    if ((value.Parameters[i].Name != null) && (value.Parameters[i].Name.Length > 0))
                    {
                        parameters = true;
                    }
                }

                formatter.WriteKeyword("function");
                formatter.Write("(");
                if (parameters)
                {
                    this.WriteParameterDeclarationList(value.Parameters, formatter, this.configuration);
                }
                formatter.Write(") {");

                formatter.WriteLine();
                formatter.WriteIndent();
                this.WriteBlockStatement(value.Body, formatter);
                formatter.WriteOutdent();
                formatter.WriteLine();
                formatter.Write("}");
            }

            private void WriteTypeReferenceExpression(ITypeReferenceExpression expression, IFormatter formatter)
            {
                this.WriteTypeReference(expression.Type, formatter);
            }

            private void WriteFieldReferenceExpression(IFieldReferenceExpression expression, IFormatter formatter)
            { // TODO bool escape = true;
                if (expression.Target != null)
                {
                    this.WriteTargetExpression(expression.Target, formatter);
                    formatter.Write(".");
                    // TODO escape = false;
                }
                this.WriteFieldReference(expression.Field, formatter);
            }

            private void WriteArgumentReferenceExpression(IArgumentReferenceExpression expression, IFormatter formatter)
            {
                // TODO Escape name?
                // TODO Should there be a Resovle() mechanism

                TextFormatter textFormatter = new TextFormatter();
                this.WriteParameterDeclaration(expression.Parameter.Resolve(), textFormatter, null);
                textFormatter.Write("; // Parameter");
                if (expression.Parameter.Name != null)
                {
                    this.WriteReference(expression.Parameter.Name, formatter, textFormatter.ToString(), null);
                }
            }

            private void WriteArgumentListExpression(IArgumentListExpression expression, IFormatter formatter)
            {
                formatter.WriteKeyword("__arglist");
            }

            private void WriteVariableReferenceExpression(IVariableReferenceExpression expression, IFormatter formatter)
            {
                this.WriteVariableReference(expression.Variable, formatter);
            }

            private void WriteVariableReference(IVariableReference value, IFormatter formatter)
            {
                IVariableDeclaration variableDeclaration = value.Resolve();

                TextFormatter textFormatter = new TextFormatter();
                this.WriteVariableDeclaration(variableDeclaration, textFormatter);
                textFormatter.Write(" // Local Variable");

                formatter.WriteReference(variableDeclaration.Name, textFormatter.ToString(), null);
            }

            private void WritePropertyIndexerExpression(IPropertyIndexerExpression expression, IFormatter formatter)
            {
                this.WriteTargetExpression(expression.Target, formatter);
                formatter.Write("(");

                bool first = true;

                foreach (IExpression index in expression.Indices)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        formatter.Write(", ");
                    }

                    this.WriteExpression(index, formatter);
                }

                formatter.Write(")");
            }

            private void WriteArrayIndexerExpression(IArrayIndexerExpression expression, IFormatter formatter)
            {
                this.WriteTargetExpression(expression.Target, formatter);
                formatter.Write("[");

                for (int i = 0; i < expression.Indices.Count; i++)
                {
                    if (i != 0)
                    {
                        formatter.Write(", ");
                    }

                    this.WriteExpression(expression.Indices[i], formatter);
                }

                formatter.Write("]");
            }

            private void WriteMethodInvokeExpression(IMethodInvokeExpression expression, IFormatter formatter)
            {
                IMethodReferenceExpression methodReferenceExpression = expression.Method as IMethodReferenceExpression;
                if (methodReferenceExpression != null)
                    this.WriteMethodReferenceExpression(methodReferenceExpression, formatter);
                else
                {
                    formatter.Write("(");
                    this.WriteExpression(expression.Method, formatter);
                    formatter.Write("^");
                    formatter.Write(")");
                }

                formatter.Write("(");
                this.WriteExpressionList(expression.Arguments, formatter);
                formatter.Write(")");
            }


            private void WriteMethodReferenceExpression(IMethodReferenceExpression expression, IFormatter formatter)
            { // TODO bool escape = true;
                if (expression.Target != null)
                { // TODO escape = false;
                    if (expression.Target is IBinaryExpression)
                    {
                        formatter.Write("(");
                        this.WriteExpression(expression.Target, formatter);
                        formatter.Write(")");
                    }
                    else
                    {
                        //formatter.WriteComment("/* " + expression.Target.GetType() + " */");
                        this.WriteTargetExpression(expression.Target, formatter);
                    }

                    formatter.Write(".");

                }
                this.WriteMethodReference(expression.Method, formatter);
            }

            private void WriteEventReferenceExpression(IEventReferenceExpression expression, IFormatter formatter)
            { // TODO bool escape = true;
                if (expression.Target != null)
                { // TODO escape = false;
                    this.WriteTargetExpression(expression.Target, formatter);
                    formatter.Write(".");
                }
                this.WriteEventReference(expression.Event, formatter);
            }

            private void WriteDelegateInvokeExpression(IDelegateInvokeExpression expression, IFormatter formatter)
            {
                if (expression.Target != null)
                {
                    this.WriteTargetExpression(expression.Target, formatter);
                }

                formatter.Write("(");
                this.WriteExpressionList(expression.Arguments, formatter);
                formatter.Write(")");
            }

            private void WriteObjectCreateExpression(IObjectCreateExpression value, IFormatter formatter)
            {
                formatter.Write("(");
                formatter.WriteKeyword("new");
                formatter.Write(" ");

                if (value.Constructor != null)
                {
                    this.WriteTypeReference((ITypeReference)value.Type, formatter, this.GetMethodReferenceDescription(value.Constructor), value.Constructor);
                }
                else
                {
                    this.WriteType(value.Type, formatter);
                }

                formatter.Write("()).ctor");

                formatter.Write("(");
                this.WriteExpressionList(value.Arguments, formatter);
                formatter.Write(")");

                IBlockExpression initializer = value.Initializer as IBlockExpression;
                if ((initializer != null) && (initializer.Expressions.Count > 0))
                {
                    formatter.Write(" ");
                    this.WriteExpression(initializer, formatter);
                }
            }

            private void WritePropertyReferenceExpression(IPropertyReferenceExpression expression, IFormatter formatter)
            { // TODO bool escape = true;
                if (expression.Target != null)
                { // TODO escape = false;
                    this.WriteTargetExpression(expression.Target, formatter);
                    formatter.Write(".");
                }
                var g = expression.Property.Resolve().GetMethod;
                WriteMethodReference(g, formatter);
                formatter.Write("()");
            }

            private void WriteThisReferenceExpression(IThisReferenceExpression expression, IFormatter formatter)
            {
                formatter.WriteKeyword("this");
            }

            private void WriteAddressOfExpression(IAddressOfExpression expression, IFormatter formatter)
            {
                formatter.Write("[");
                this.WriteExpression(expression.Expression, formatter);
                formatter.Write("]");
            }

            private void WriteAddressReferenceExpression(IAddressReferenceExpression expression, IFormatter formatter)
            {
                formatter.Write("[");
                this.WriteExpression(expression.Expression, formatter);
                formatter.Write("]");
            }

            private void WriteAddressOutExpression(IAddressOutExpression expression, IFormatter formatter)
            {
                formatter.Write("[");
                this.WriteExpression(expression.Expression, formatter);
                formatter.Write("]");
            }

            private void WriteAddressDereferenceExpression(IAddressDereferenceExpression expression, IFormatter formatter)
            {
                IAddressOfExpression addressOf = expression.Expression as IAddressOfExpression;
                if (addressOf != null)
                {
                    this.WriteExpression(addressOf.Expression, formatter);
                }
                else
                {
                    // formatter.Write("*(");
                    this.WriteExpression(expression.Expression, formatter);
                    // formatter.Write(")");
                }
            }

            private void WriteSizeOfExpression(ISizeOfExpression expression, IFormatter formatter)
            {
                formatter.WriteKeyword("sizeof");
                formatter.Write("(");
                this.WriteType(expression.Type, formatter);
                formatter.Write(")");
            }

            private void WriteStackAllocateExpression(IStackAllocateExpression expression, IFormatter formatter)
            {
                formatter.WriteKeyword("stackalloc");
                formatter.Write(" ");
                this.WriteType(expression.Type, formatter);
                formatter.Write("[");
                this.WriteExpression(expression.Expression, formatter);
                formatter.Write("]");
            }

            private void WriteLambdaExpression(ILambdaExpression value, IFormatter formatter)
            {
                formatter.WriteKeyword("function");
                formatter.Write("(");

                for (int i = 0; i < value.Parameters.Count; i++)
                {
                    if (i != 0)
                    {
                        formatter.Write(", ");
                    }

                    // this.WriteVariableIdentifier(value.Parameters[i].Variable.Identifier, formatter);
                    this.WriteDeclaration(value.Parameters[i].Name, formatter);
                }

                formatter.Write(")");

                formatter.Write(" { ");

                formatter.WriteKeyword("return");

                formatter.Write(" ");

                this.WriteExpression(value.Body, formatter);

                formatter.Write("; }");
            }

            private void WriteQueryExpression(IQueryExpression value, IFormatter formatter)
            {
                formatter.Write("(");

                this.WriteFromClause(value.From, formatter);

                if ((value.Body.Clauses.Count > 0) || (value.Body.Continuation != null))
                {
                    formatter.WriteLine();
                    formatter.WriteIndent();
                }
                else
                {
                    formatter.Write(" ");
                }

                this.WriteQueryBody(value.Body, formatter);

                formatter.Write(")");

                if ((value.Body.Clauses.Count > 0) || (value.Body.Continuation != null))
                {
                    formatter.WriteOutdent();
                }
            }

            private void WriteQueryBody(IQueryBody value, IFormatter formatter)
            {
                // from | where | let | join | orderby
                for (int i = 0; i < value.Clauses.Count; i++)
                {
                    this.WriteQueryClause(value.Clauses[i], formatter);
                    formatter.WriteLine();
                }

                // select | group
                this.WriteQueryOperation(value.Operation, formatter);

                // into
                if (value.Continuation != null)
                {
                    formatter.Write(" ");
                    this.WriteQueryContinuation(value.Continuation, formatter);
                }
            }

            private void WriteQueryContinuation(IQueryContinuation value, IFormatter formatter)
            {
                formatter.WriteKeyword("into");
                formatter.Write(" ");
                this.WriteDeclaration(value.Variable.Name, formatter);
                formatter.WriteLine();
                this.WriteQueryBody(value.Body, formatter);
            }

            private void WriteQueryClause(IQueryClause value, IFormatter formatter)
            {
                if (value is IWhereClause)
                {
                    this.WriteWhereClause(value as IWhereClause, formatter);
                    return;
                }

                if (value is ILetClause)
                {
                    this.WriteLetClause(value as ILetClause, formatter);
                    return;
                }

                if (value is IFromClause)
                {
                    this.WriteFromClause(value as IFromClause, formatter);
                    return;
                }

                if (value is IJoinClause)
                {
                    this.WriteJoinClause(value as IJoinClause, formatter);
                    return;
                }

                if (value is IOrderClause)
                {
                    this.WriteOrderClause(value as IOrderClause, formatter);
                    return;
                }

                throw new NotSupportedException();
            }

            private void WriteQueryOperation(IQueryOperation value, IFormatter formatter)
            {
                if (value is ISelectOperation)
                {
                    this.WriteSelectOperation(value as ISelectOperation, formatter);
                    return;
                }

                if (value is IGroupOperation)
                {
                    this.WriteGroupOperation(value as IGroupOperation, formatter);
                    return;
                }

                throw new NotSupportedException();
            }

            private void WriteFromClause(IFromClause value, IFormatter formatter)
            {
                formatter.WriteKeyword("from");
                formatter.Write(" ");
                this.WriteDeclaration(value.Variable.Name, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("in");
                formatter.Write(" ");
                this.WriteExpression(value.Expression, formatter);
            }

            private void WriteWhereClause(IWhereClause value, IFormatter formatter)
            {
                formatter.WriteKeyword("where");
                formatter.Write(" ");
                this.WriteExpression(value.Expression, formatter);
            }

            private void WriteLetClause(ILetClause value, IFormatter formatter)
            {
                formatter.WriteKeyword("let");
                formatter.Write(" ");
                this.WriteDeclaration(value.Variable.Name, formatter);
                formatter.Write(" = ");
                this.WriteExpression(value.Expression, formatter);
            }

            private void WriteJoinClause(IJoinClause value, IFormatter formatter)
            {
                formatter.WriteKeyword("join");
                formatter.Write(" ");
                this.WriteDeclaration(value.Variable.Name, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("in");
                formatter.Write(" ");
                this.WriteExpression(value.In, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("on");
                formatter.Write(" ");
                this.WriteExpression(value.On, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("equals");
                formatter.Write(" ");
                this.WriteExpression(value.Equality, formatter);

                if (value.Into != null)
                {
                    formatter.Write(" ");
                    formatter.WriteKeyword("into");
                    formatter.Write(" ");
                    this.WriteDeclaration(value.Into.Name, formatter);
                }
            }

            private void WriteOrderClause(IOrderClause value, IFormatter formatter)
            {
                formatter.WriteKeyword("orderby");
                formatter.Write(" ");

                var ed = value.ExpressionAndDirections[0];

                this.WriteExpression(ed.Expression, formatter);

                if (ed.Direction == OrderDirection.Descending)
                {
                    formatter.Write(" ");
                    formatter.WriteKeyword("descending");
                }
            }

            private void WriteSelectOperation(ISelectOperation value, IFormatter formatter)
            {
                formatter.WriteKeyword("select");
                formatter.Write(" ");
                this.WriteExpression(value.Expression, formatter);
            }

            private void WriteGroupOperation(IGroupOperation value, IFormatter formatter)
            {
                formatter.WriteKeyword("group");
                formatter.Write(" ");
                this.WriteExpression(value.Item, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("by");
                formatter.Write(" ");
                this.WriteExpression(value.Key, formatter);
            }

            private void WriteSnippetExpression(ISnippetExpression expression, IFormatter formatter)
            {
                formatter.WriteComment(expression.Value);
            }

            private void WriteUnaryExpression(IUnaryExpression expression, IFormatter formatter)
            {
                switch (expression.Operator)
                {
                    case UnaryOperator.BitwiseNot:
                        formatter.WriteKeyword("!");
                        this.WriteExpression(expression.Expression, formatter);
                        break;

                    case UnaryOperator.BooleanNot:
                        formatter.WriteKeyword("!");
                        this.WriteExpression(expression.Expression, formatter);
                        break;

                    case UnaryOperator.Negate:
                        formatter.Write("-");
                        this.WriteExpression(expression.Expression, formatter);
                        break;

                    case UnaryOperator.PreIncrement:
                        formatter.Write("++");
                        this.WriteExpression(expression.Expression, formatter);
                        break;

                    case UnaryOperator.PreDecrement:
                        formatter.Write("--");
                        this.WriteExpression(expression.Expression, formatter);
                        break;

                    case UnaryOperator.PostIncrement:
                        this.WriteExpression(expression.Expression, formatter);
                        formatter.Write("++");
                        break;

                    case UnaryOperator.PostDecrement:
                        this.WriteExpression(expression.Expression, formatter);
                        formatter.Write("--");
                        break;

                    default:
                        throw new NotSupportedException(expression.Operator.ToString());
                }
            }

            private void WriteBinaryExpression(IBinaryExpression expression, IFormatter formatter)
            {
                formatter.Write("(");
                this.WriteExpression(expression.Left, formatter);
                formatter.Write(" ");
                this.WriteBinaryOperator(expression.Operator, formatter);
                formatter.Write(" ");
                this.WriteExpression(expression.Right, formatter);
                formatter.Write(")");
            }

            private void WriteBinaryOperator(BinaryOperator operatorType, IFormatter formatter)
            {
                switch (operatorType)
                {
                    case BinaryOperator.Add:
                        formatter.Write("+");
                        break;

                    case BinaryOperator.Subtract:
                        formatter.Write("-");
                        break;

                    case BinaryOperator.Multiply:
                        formatter.Write("*");
                        break;

                    case BinaryOperator.Divide:
                        formatter.WriteKeyword("/");
                        break;

                    case BinaryOperator.Modulus:
                        formatter.WriteKeyword("%");
                        break;

                    case BinaryOperator.ShiftLeft:
                        formatter.WriteKeyword("<<");
                        break;

                    case BinaryOperator.ShiftRight:
                        formatter.WriteKeyword(">>");
                        break;

                    case BinaryOperator.ValueInequality:
                    case BinaryOperator.IdentityInequality:
                        formatter.Write("!==");
                        break;

                    case BinaryOperator.ValueEquality:
                    case BinaryOperator.IdentityEquality:
                        formatter.Write("===");
                        break;

                    case BinaryOperator.BitwiseOr:
                        formatter.WriteKeyword("|");
                        break;

                    case BinaryOperator.BitwiseAnd:
                        formatter.WriteKeyword("&");
                        break;

                    case BinaryOperator.BitwiseExclusiveOr:
                        formatter.WriteKeyword("^");
                        break;

                    case BinaryOperator.BooleanOr:
                        formatter.WriteKeyword("||");
                        break;

                    case BinaryOperator.BooleanAnd:
                        formatter.WriteKeyword("&&");
                        break;

                    case BinaryOperator.LessThan:
                        formatter.Write("<");
                        break;

                    case BinaryOperator.LessThanOrEqual:
                        formatter.Write("<=");
                        break;

                    case BinaryOperator.GreaterThan:
                        formatter.Write(">");
                        break;

                    case BinaryOperator.GreaterThanOrEqual:
                        formatter.Write(">=");
                        break;

                    default:
                        throw new NotSupportedException(operatorType.ToString());
                }
            }

            private void WriteLiteralExpression(ILiteralExpression value, IFormatter formatter)
            {
                if (value.Value == null)
                {
                    formatter.WriteLiteral("null");
                }
                else if (value.Value is char)
                {
                    string text = new string(new char[] { (char)value.Value });
                    text = this.QuoteLiteralExpression(text);
                    formatter.WriteLiteral("\"" + text + "\"");
                }
                else if (value.Value is string)
                {
                    string text = (string)value.Value;
                    text = this.QuoteLiteralExpression(text);
                    formatter.WriteLiteral("\"" + text + "\"");
                }
                else if (value.Value is byte)
                {
                    this.WriteNumber((byte)value.Value, formatter);
                }
                else if (value.Value is sbyte)
                {
                    this.WriteNumber((sbyte)value.Value, formatter);
                }
                else if (value.Value is short)
                {
                    this.WriteNumber((short)value.Value, formatter);
                }
                else if (value.Value is ushort)
                {
                    this.WriteNumber((ushort)value.Value, formatter);
                }
                else if (value.Value is int)
                {
                    this.WriteNumber((int)value.Value, formatter);
                }
                else if (value.Value is uint)
                {
                    this.WriteNumber((uint)value.Value, formatter);
                }
                else if (value.Value is long)
                {
                    this.WriteNumber((long)value.Value, formatter);
                }
                else if (value.Value is ulong)
                {
                    this.WriteNumber((ulong)value.Value, formatter);
                }
                else if (value.Value is float)
                {
                    // TODO
                    formatter.WriteLiteral(((float)value.Value).ToString(CultureInfo.InvariantCulture));
                }
                else if (value.Value is double)
                {
                    // TODO
                    formatter.WriteLiteral(((double)value.Value).ToString("R", CultureInfo.InvariantCulture));
                }
                else if (value.Value is decimal)
                {
                    formatter.WriteLiteral(((decimal)value.Value).ToString(CultureInfo.InvariantCulture));
                }
                else if (value.Value is bool)
                {
                    formatter.WriteLiteral(((bool)value.Value) ? "true" : "false");
                }
                /*
                else if (expression.Value is byte[])
                {
                    formatter.WriteComment("{ ");

                    byte[] bytes = (byte[])expression.Value;
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        if (i != 0)
                        {
                            formatter.Write(", ");
                        }

                        formatter.WriteComment("0x" + bytes[i].ToString("X2", CultureInfo.InvariantCulture));
                    }

                    formatter.WriteComment(" }");
                }
                */
                else
                {
                    throw new ArgumentException("expression");
                }
            }

            private void WriteNumber(IConvertible value, IFormatter formatter)
            {
                IFormattable formattable = (IFormattable)value;

                switch (this.GetNumberFormat(value))
                {
                    case NumberFormat.Decimal:
                        formatter.WriteLiteral(formattable.ToString(null, CultureInfo.InvariantCulture));
                        break;

                    case NumberFormat.Hexadecimal:
                        formatter.WriteLiteral("0x" + formattable.ToString("x", CultureInfo.InvariantCulture));
                        break;
                }
            }

            private NumberFormat GetNumberFormat(IConvertible value)
            {
                NumberFormat format = this.numberFormat;
                if (format == NumberFormat.Auto)
                {
                    long number = (value is ulong) ? (long)(ulong)value : value.ToInt64(CultureInfo.InvariantCulture);

                    if (number < 16)
                    {
                        return NumberFormat.Decimal;
                    }

                    if (((number % 10) == 0) && (number < 1000))
                    {
                        return NumberFormat.Decimal;
                    }

                    return NumberFormat.Hexadecimal;
                }

                return format;
            }

            private void WriteTypeReference(ITypeReference typeReference, IFormatter formatter)
            {
                this.WriteType(typeReference, formatter);
            }

            private void WriteTypeReference(ITypeReference typeReference, IFormatter formatter, string description, object target)
            {
                string name = typeReference.Namespace + "." + typeReference.Name;

                // TODO mscorlib test
                if (typeReference.Namespace == "System")
                {
                    if (specialTypeNames.Contains(name))
                    {
                        name = (string)specialTypeNames[name];
                    }
                }

                ITypeReference genericType = typeReference.GenericType;
                if (genericType != null)
                {
                    this.WriteReference(name, formatter, description, genericType);
                    //this.WriteGenericArgumentList(typeReference.GenericArguments, formatter);
                }
                else
                {
                    this.WriteReference(name, formatter, description, target);
                }
            }

            private void WriteFieldReference(IFieldReference fieldReference, IFormatter formatter)
            {
                // TODO Escape?
                this.WriteReference(fieldReference.Name, formatter, this.GetFieldReferenceDescription(fieldReference), fieldReference);
            }

            private void WriteMethodReference(IMethodReference methodReference, IFormatter formatter)
            {
                // TODO Escape?

                IMethodReference genericMethod = methodReference.GenericMethod;
                if (genericMethod != null)
                {
                    this.WriteReference(methodReference.Name, formatter, this.GetMethodReferenceDescription(methodReference), genericMethod);
                    //this.WriteGenericArgumentList(methodReference.GenericArguments, formatter);
                }
                else
                {
                    this.WriteReference(methodReference.Name, formatter, this.GetMethodReferenceDescription(methodReference), methodReference);
                }
            }


            private void WritePropertyReference(IPropertyReference propertyReference, IFormatter formatter)
            {
                // TODO Escape?
                this.WriteReference(propertyReference.Name, formatter, this.GetPropertyReferenceDescription(propertyReference), propertyReference);
            }

            private void WriteEventReference(IEventReference eventReference, IFormatter formatter)
            {
                // TODO Escape?
                this.WriteReference(eventReference.Name, formatter, this.GetEventReferenceDescription(eventReference), eventReference);
            }

            #endregion

            #region Statement

            public void WriteStatement(IStatement value)
            {
                this.WriteStatement(value, this.formatter);
            }

            private void WriteStatement(IStatement value, IFormatter formatter)
            {
                WriteStatement(value, formatter, false);
            }

            private void WriteStatement(IStatement value, IFormatter formatter, bool lastStatement)
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                if (value is IBlockStatement)
                {
                    this.WriteBlockStatement(value as IBlockStatement, formatter);
                    return;
                }

                if (value is IExpressionStatement)
                {
                    this.WriteExpressionStatement(value as IExpressionStatement, formatter);
                    return;
                }

                if (value is IGotoStatement)
                {
                    this.WriteGotoStatement(value as IGotoStatement, formatter);
                    return;
                }

                if (value is ILabeledStatement)
                {
                    this.WriteLabeledStatement(value as ILabeledStatement, formatter);
                    return;
                }

                if (value is IConditionStatement)
                {
                    this.WriteConditionStatement(value as IConditionStatement, formatter);
                    return;
                }

                if (value is IMethodReturnStatement)
                {
                    this.WriteMethodReturnStatement(value as IMethodReturnStatement, formatter, lastStatement);
                    return;
                }

                if (value is IForStatement)
                {
                    this.WriteForStatement(value as IForStatement, formatter);
                    return;
                }

                if (value is IForEachStatement)
                {
                    this.WriteForEachStatement(value as IForEachStatement, formatter);
                    return;
                }

                if (value is IUsingStatement)
                {
                    this.WriteUsingStatement(value as IUsingStatement, formatter);
                    return;
                }

                if (value is IFixedStatement)
                {
                    this.WriteFixedStatement(value as IFixedStatement, formatter);
                    return;
                }

                if (value is IWhileStatement)
                {
                    this.WriteWhileStatement(value as IWhileStatement, formatter);
                    return;
                }

                if (value is IDoStatement)
                {
                    this.WriteDoStatement(value as IDoStatement, formatter);
                    return;
                }

                if (value is ITryCatchFinallyStatement)
                {
                    this.WriteTryCatchFinallyStatement(value as ITryCatchFinallyStatement, formatter);
                    return;
                }

                if (value is IThrowExceptionStatement)
                {
                    this.WriteThrowExceptionStatement(value as IThrowExceptionStatement, formatter);
                    return;
                }

                if (value is IAttachEventStatement)
                {
                    this.WriteAttachEventStatement(value as IAttachEventStatement, formatter);
                    return;
                }

                if (value is IRemoveEventStatement)
                {
                    this.WriteRemoveEventStatement(value as IRemoveEventStatement, formatter);
                    return;
                }

                if (value is ISwitchStatement)
                {
                    this.WriteSwitchStatement(value as ISwitchStatement, formatter);
                    return;
                }

                if (value is IBreakStatement)
                {
                    this.WriteBreakStatement(value as IBreakStatement, formatter);
                    return;
                }

                if (value is IContinueStatement)
                {
                    this.WriteContinueStatement(value as IContinueStatement, formatter);
                    return;
                }

                if (value is IMemoryCopyStatement)
                {
                    this.WriteMemoryCopyStatement(value as IMemoryCopyStatement, formatter);
                    return;
                }

                if (value is IMemoryInitializeStatement)
                {
                    this.WriteMemoryInitializeStatement(value as IMemoryInitializeStatement, formatter);
                    return;
                }

                if (value is IDebugBreakStatement)
                {
                    this.WriteDebugBreakStatement(value as IDebugBreakStatement, formatter);
                    return;
                }

                if (value is ILockStatement)
                {
                    this.WriteLockStatement(value as ILockStatement, formatter);
                    return;
                }

                if (value is ICommentStatement)
                {
                    this.WriteCommentStatement(value as ICommentStatement, formatter);
                    return;
                }

                throw new ArgumentException("Invalid statement type `" + value.GetType() + "`.", "value");
            }

            private void WritePendingOutdent(IFormatter formatter)
            {
                if (pendingOutdent > 0)
                {
                    formatter.WriteOutdent();
                    pendingOutdent = 0;
                }
            }

            private void MakePendingOutdent()
            {
                pendingOutdent = 1;
            }

            private void WriteStatementSeparator(IFormatter formatter)
            {
                if (this.firstStmt)
                    this.firstStmt = false;
                else
                    if (!this.forLoop)
                    {
                        formatter.Write(";");
                        formatter.WriteLine();
                    }
                WritePendingOutdent(formatter);
            }

            private void WriteBlockStatement(IBlockStatement statement, IFormatter formatter)
            {
                blockStatementLevel++;
                if (statement.Statements.Count > 0)
                {
                    this.WriteStatementList(statement.Statements, formatter);
                    this.WriteStatementSeparator(formatter);
                }
                blockStatementLevel++;
            }

            private void WriteStatementList(IStatementCollection statements, IFormatter formatter)
            {
                this.firstStmt = true;
                // put Delphi Loop detection here for now
                //			DetectDelphiIterationStatement1(statements);
                //			DetectDelphiIterationStatement2(statements);
                //
                for (int i = 0; i < statements.Count; i++)
                {
                    this.WriteStatement(statements[i], formatter, (i == statements.Count - 1));
                }
            }


            private void WriteMemoryCopyStatement(IMemoryCopyStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("memcpy");
                formatter.Write("(");
                this.WriteExpression(statement.Source, formatter);
                formatter.Write(", ");
                this.WriteExpression(statement.Destination, formatter);
                formatter.Write(", ");
                this.WriteExpression(statement.Length, formatter);
                formatter.Write(")");
            }

            private void WriteMemoryInitializeStatement(IMemoryInitializeStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("meminit");
                formatter.Write("(");
                this.WriteExpression(statement.Offset, formatter);
                formatter.Write(", ");
                this.WriteExpression(statement.Value, formatter);
                formatter.Write(", ");
                this.WriteExpression(statement.Length, formatter);
                formatter.Write(")");
            }

            private void WriteDebugBreakStatement(IDebugBreakStatement value, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("debug");
            }

            private void WriteLockStatement(ILockStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("lock");
                formatter.Write(" ");
                formatter.Write("(");
                this.WriteExpression(statement.Expression, formatter);
                formatter.Write(")");
                formatter.WriteLine();

                formatter.WriteKeyword("begin");
                formatter.WriteIndent();

                if (statement.Body != null)
                {
                    this.WriteStatement(statement.Body, formatter);
                }

                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.WriteKeyword("end");
            }


            /*
            //-------------------------------------------------
                private void DetectDelphiIterationStatement1(IStatementCollection statements)
                {
        // Delphi-style dynamic for-loop:
        //			for i := k to j do
        //				Debug.Writeline('For expr');

        //			 num2 = num5; 									 // init1
        //			 num1 = num6; 									 // init2
        //			 if (num2 < num1) 							 // condTop
        //			 {
        //					goto L_0027;								 // gotoBottom
        //			 }
        //			 num2 = (num2 + 1); 						 // incr1
        //			 _0015: 												 // labelTop
        //			 Debug.WriteLine("For expr");
        //			 num1 = (num1 + 1); 						 // incr2
        //			 if (num1 != num2)							 // condBottom
        //			 {
        //					goto L_0015;								 // gotoTop
        //			 }
        //			 _0027: 												 // labelBottom
        //}
                    for (int i = 0; i < statements.Count-4; i++)
                    {
                        IAssignStatement init1 = statements[i] as IAssignStatement; 					 if (init1==null) continue;
                        IAssignStatement init2 = statements[i+1] as IAssignStatement; 				 if (init2==null) continue;
                        IConditionStatement condTop = statements[i+2] as IConditionStatement;  if (condTop==null) continue;
                        IAssignStatement incr1 = statements[i+3] as IAssignStatement; 				 if (incr1==null) continue;
                        ILabeledStatement labelTop = statements[i+4] as ILabeledStatement;		 if (labelTop==null) continue;

                        if ((init1 != null) && (init2 != null) && (incr1 != null) && (condTop != null) && (labelTop != null)
                        // && (this.blockTable[labelTop].Length == 1)
                        && (condTop.Then.Statements.Count == 1) && (condTop.Else.Statements.Count == 0))
                        {
                            IBinaryExpression condTopExpr = condTop.Condition as IBinaryExpression;
                            IGotoStatement gotoBottom = condTop.Then.Statements[0] as IGotoStatement;
                            if ((condTopExpr != null) && (gotoBottom != null))
                            {
                                IVariableReferenceExpression condTopLeftVar = condTopExpr.Left as IVariableReferenceExpression;
                                IVariableReferenceExpression condTopRightVar = condTopExpr.Right as IVariableReferenceExpression;
                                IVariableReferenceExpression init1Var = init1.Target as IVariableReferenceExpression;
                                IVariableReferenceExpression init2Var = init2.Target as IVariableReferenceExpression;
                                IVariableReferenceExpression incr1Var = incr1.Target as IVariableReferenceExpression;
                                if ((condTopLeftVar != null)	&& (condTopRightVar != null)	&& (init1Var != null) && (init1Var != null) &&
                                        (condTopLeftVar.Variable == init1Var.Variable) && (condTopRightVar.Variable == init2Var.Variable) &&
                                        (incr1Var != null) &&(incr1Var.Variable == init1Var.Variable))
                                {
                                    // search for the loop-back test, goto top
                                    for (int j = i + 5; j < statements.Count-2; j++)
                                    {
                                        IAssignStatement incr2 = statements[j] as IAssignStatement;
                                        IConditionStatement condBottom = statements[j+1] as IConditionStatement;
                                        ILabeledStatement labelBottom = statements[j+2] as ILabeledStatement;
                                        if ((incr2 != null) && (condBottom != null) && (labelBottom != null)
                                        //&& (this.blockTable[labelBottom].Length == 1)
                                        && (condBottom.Then.Statements.Count == 1) && (condBottom.Else.Statements.Count == 0))
                                        {
                                            IGotoStatement gotoTop = condBottom.Then.Statements[0] as IGotoStatement;
                                            IVariableReferenceExpression incr2Var = incr2.Target as IVariableReferenceExpression;
                                            IBinaryExpression condBottomExpr = condBottom.Condition as IBinaryExpression;
                                            // TODO: check condBottom.Operator vs condTop.Operator
                                            if ((gotoTop != null) && (gotoTop.Name == labelTop.Name) &&
                                                    (condBottomExpr != null) && (incr2Var != null) && (incr2Var.Variable == init2Var.Variable))
                                            {
                                                IVariableReferenceExpression condBottomLeftVar = condBottomExpr.Left as IVariableReferenceExpression;
                                                IVariableReferenceExpression condBottomRightVar = condBottomExpr.Right as IVariableReferenceExpression;
                                                if ((condBottomLeftVar != null)  && (condBottomRightVar != null) &&
                                                        (condBottomLeftVar.Variable == init2Var.Variable) && (condBottomRightVar.Variable == init1Var.Variable))
                                                {
        // don't know how to do this yet
        //											this.blockTable.RemoveGotoStatement(gotoBottom);
        //											this.blockTable.RemoveGotoStatement(gotoTop);

                                                    // Replace RHS of condition with full, pre-computed expression
                                                    condBottomExpr.Right = incr1.Expression;

                                                    IWhileStatement whileStatement = new WhileStatement();
                                                    whileStatement.Condition = this.InverseBooleanExpression(condTop.Condition); // condBottom.Condition;
                                                    whileStatement.Body = new BlockStatement();
                                                    statements.RemoveAt(j+1);  // Remove condBottom
                                                    statements.RemoveAt(i+3);  // Remove incr1
                                                    statements.RemoveAt(i+2);  // Remove condTop

                                                    for (int k = j - 2; k > i+1; k--)
                                                    {
                                                        whileStatement.Body.Statements.Insert(0, statements[k]);
                                                        statements.RemoveAt(k);
                                                    }
                                                    statements.Insert(i+2, whileStatement, formatter);

                                                    // this.OptimizeStatementList(whileStatement.Block.Statements);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            */







































































































            /*
                private void DetectDelphiIterationStatement2(IStatementCollection statements)
                {
        // Delphi-style constant for-loop:
        //	for i := 0 to 10 do
        //		Use(i + j);
        //		-----------
        //		int num1;
        //			 num1 = 0;								 // init
        //			 label _0005: 						 // labelTop
        //			 Unit.Use((num1 + num2));
        //			 num1 = (num1 + 1);
        //			 if (num1 != 11)					 // condition
        //			 {
        //							 goto L_0005; 		 // gotoTop
        //			 }
                    for (int i = 0; i < statements.Count-1; i++)
                    {
                        IAssignStatement init = statements[i] as IAssignStatement;
                        ILabeledStatement labelTop = statements[i+1] as ILabeledStatement;
                        if ((init != null) && (labelTop != null)
                        // && (this.blockTable[labelTop].Length == 1)
                        )
                        { // search for the loop-back test, goto top
                            for (int j = i + 2; j < statements.Count; j++)
                            { IConditionStatement condition = statements[j] as IConditionStatement;
                                if ((condition != null) && (condition.Then.Statements.Count == 1) && (condition.Else.Statements.Count == 0))
                                { IBinaryExpression condExpr = condition.Condition as IBinaryExpression;
                                    if ((condExpr != null) )
                                    { IVariableReferenceExpression checkVar = condExpr.Left as IVariableReferenceExpression;
                                        IVariableReferenceExpression initVar = init.Target as IVariableReferenceExpression;
                                        if ((checkVar != null)	&& (initVar != null) && (checkVar.Variable == initVar.Variable))
                                        { IGotoStatement gotoTop = condition.Then.Statements[0] as IGotoStatement;
                                            if ((gotoTop != null) && (gotoTop.Name == labelTop.Name))
                                            { // this.blockTable.RemoveGotoStatement(gotoTop);
                                                IWhileStatement whileStatement = new WhileStatement();
                                                whileStatement.Condition = condition.Condition;
                                                whileStatement.Body = new BlockStatement();
                                                statements.RemoveAt(j);
                                                for (int k = j - 1; k > i; k--)
                                                { whileStatement.Body.Statements.Insert(0, statements[k]);
                                                    statements.RemoveAt(k);
                                                }
                                                statements.Insert(i+1, whileStatement, formatter);
                                                // this.OptimizeStatementList(whileStatement.Block.Statements);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            */






















































            internal static IExpression InverseBooleanExpression(IExpression expression)
            {
                IBinaryExpression binaryExpression = expression as IBinaryExpression;
                if (binaryExpression != null)
                {
                    switch (binaryExpression.Operator)
                    {
                        case BinaryOperator.GreaterThan:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.LessThanOrEqual;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.GreaterThanOrEqual:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.LessThan;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.LessThan:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.GreaterThanOrEqual;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.LessThanOrEqual:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.GreaterThan;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.IdentityEquality:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.IdentityInequality;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.IdentityInequality:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.IdentityEquality;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.ValueInequality:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.ValueEquality;
                                target.Right = binaryExpression.Right;
                                return target;
                            }
                        case BinaryOperator.ValueEquality:
                            {
                                IBinaryExpression target = new BinaryExpression();
                                target.Left = binaryExpression.Left;
                                target.Operator = BinaryOperator.ValueInequality;
                                target.Right = binaryExpression.Right;
                                return target;
                            }

                        case BinaryOperator.BooleanAnd: // De Morgan
                            {
                                IExpression left = InverseBooleanExpression(binaryExpression.Left);
                                IExpression right = InverseBooleanExpression(binaryExpression.Right);
                                if ((left != null) && (right != null))
                                {
                                    IBinaryExpression target = new BinaryExpression();
                                    target.Left = left;
                                    target.Operator = BinaryOperator.BooleanOr;
                                    target.Right = right;
                                    return target;
                                }
                            }
                            break;


                        case BinaryOperator.BooleanOr: // De Morgan
                            {
                                IExpression left = InverseBooleanExpression(binaryExpression.Left);
                                IExpression right = InverseBooleanExpression(binaryExpression.Right);
                                if ((left != null) && (right != null))
                                {
                                    IBinaryExpression target = new BinaryExpression();
                                    target.Left = left;
                                    target.Operator = BinaryOperator.BooleanAnd;
                                    target.Right = right;
                                    return target;
                                }
                            }
                            break;
                    }
                }

                IUnaryExpression unaryExpression = expression as IUnaryExpression;
                if (unaryExpression != null)
                {
                    if (unaryExpression.Operator == UnaryOperator.BooleanNot)
                    {
                        return unaryExpression.Expression;
                    }
                }

                IUnaryExpression unaryOperator = new UnaryExpression();
                unaryOperator.Operator = UnaryOperator.BooleanNot;
                unaryOperator.Expression = expression;
                return unaryOperator;
            }

            //-------------------------------------------
            // this writes one line of variable declaration and sets the hasvar flag to true
            //  if it was false, put out the "var" definition line
            private void WriteVariableListEntry(IVariableDeclaration variable, IFormatter formatter, ref bool hasvar)
            {
                if (variable != null)
                    this.WriteVariableDeclaration(variable, formatter);
            }

            private void WriteVariableList(IVariableDeclarationExpression expression, IFormatter formatter, ref bool hasvar)
            {
                if (expression != null)
                    WriteVariableListEntry(expression.Variable, formatter, ref hasvar);
            }

            private void WriteVariableList(IStatement statement, IFormatter formatter, ref bool hasvar)
            {
                IBlockStatement blockStatement = statement as IBlockStatement;
                if (blockStatement != null)
                {
                    WriteVariableList(blockStatement.Statements, formatter, ref hasvar);
                    return;
                }

                ILabeledStatement labeledStatement = statement as ILabeledStatement;
                if (labeledStatement != null)
                {
                    WriteVariableList(labeledStatement.Statement, formatter, ref hasvar);
                    return;
                }

                IForEachStatement forEachStatement = statement as IForEachStatement;
                if (forEachStatement != null)
                {
                    WriteVariableListEntry(forEachStatement.Variable, formatter, ref hasvar);
                    WriteVariableList(forEachStatement.Body, formatter, ref hasvar);
                    return;
                }

                IConditionStatement conditionStatement = statement as IConditionStatement;
                if (conditionStatement != null)
                {
                    WriteVariableList(conditionStatement.Then, formatter, ref hasvar);
                    WriteVariableList(conditionStatement.Else, formatter, ref hasvar);
                    return;
                }

                IForStatement forStatement = statement as IForStatement;
                if (forStatement != null)
                {
                    WriteVariableList(forStatement.Initializer, formatter, ref hasvar);
                    WriteVariableList(forStatement.Body, formatter, ref hasvar);
                    return;
                }

                ISwitchStatement switchStatement = statement as ISwitchStatement;
                if (switchStatement != null)
                {
                    foreach (ISwitchCase switchCase in switchStatement.Cases)
                        WriteVariableList(switchCase.Body, formatter, ref hasvar);
                    return;
                }

                IDoStatement doStatement = statement as IDoStatement;
                if (doStatement != null)
                {
                    WriteVariableList(doStatement.Body, formatter, ref hasvar);
                    return;
                }

                ILockStatement lockStatement = statement as ILockStatement;
                if (lockStatement != null)
                {
                    WriteVariableList(lockStatement.Body, formatter, ref hasvar);
                    return;
                }

                IWhileStatement whileStatement = statement as IWhileStatement;
                if (whileStatement != null)
                {
                    WriteVariableList(whileStatement.Body, formatter, ref hasvar);
                    return;
                }

                IFixedStatement fixedStatement = statement as IFixedStatement;
                if (fixedStatement != null)
                {
                    WriteVariableListEntry(fixedStatement.Variable, formatter, ref hasvar);
                    WriteVariableList(fixedStatement.Body, formatter, ref hasvar);
                    return;
                }

                IUsingStatement usingStatement = statement as IUsingStatement;
                if (usingStatement != null)
                {
                    IAssignExpression assignExpression = usingStatement.Expression as IAssignExpression;
                    if (assignExpression != null)
                    {
                        IVariableDeclarationExpression variableDeclarationExpression = assignExpression.Target as IVariableDeclarationExpression;
                        if (variableDeclarationExpression != null)
                        {
                            this.WriteVariableListEntry(variableDeclarationExpression.Variable, formatter, ref hasvar);
                        }
                    }

                    return;
                }

                ITryCatchFinallyStatement tryCatchFinallyStatement = statement as ITryCatchFinallyStatement;
                if (tryCatchFinallyStatement != null)
                {
                    WriteVariableList(tryCatchFinallyStatement.Try, formatter, ref hasvar);
                    foreach (ICatchClause catchClause in tryCatchFinallyStatement.CatchClauses)
                        WriteVariableList(catchClause.Body, formatter, ref hasvar);
                    WriteVariableList(tryCatchFinallyStatement.Fault, formatter, ref hasvar);
                    WriteVariableList(tryCatchFinallyStatement.Finally, formatter, ref hasvar);
                    return;
                }

                IExpressionStatement expressionStatement = statement as IExpressionStatement;
                if (expressionStatement != null)
                {
                    WriteVariableList(expressionStatement.Expression as IVariableDeclarationExpression, formatter, ref hasvar);
                    return;
                }

            }

            // write a list of variable definitions by recursing through the statements and define
            //  the corresponding variable names
            private void WriteVariableList(IStatementCollection statements, IFormatter formatter, ref bool hasvar)
            {
                foreach (IStatement statement in statements)
                    WriteVariableList(statement, formatter, ref hasvar);
            }

            private void WriteCommentStatement(ICommentStatement statement, IFormatter formatter)
            {
                this.WriteComment(statement.Comment, formatter);
            }

            private void WriteComment(IComment comment, IFormatter formatter)
            {
                string[] parts = comment.Text.Split(new char[] { '\n' });
                if (parts.Length <= 1)
                {
                    foreach (string part in parts)
                    {
                        formatter.WriteComment("// ");
                        formatter.WriteComment(part);
                        formatter.WriteLine();
                    }
                }
                else
                {
                    formatter.WriteComment("/* ");
                    formatter.WriteLine();

                    foreach (string part in parts)
                    {
                        formatter.WriteComment(part);
                        formatter.WriteLine();
                    }

                    formatter.WriteComment("*/");
                    formatter.WriteLine();
                }
            }

            private void WriteMethodReturnStatement(IMethodReturnStatement statement, IFormatter formatter, bool lastStatement)
            {
                this.WriteStatementSeparator(formatter);
                if (statement.Expression == null)
                {
                    formatter.WriteKeyword("return");
                    formatter.Write(";");
                }
                else
                {
                    formatter.WriteKeyword("return");
                    formatter.Write(" ");
                    this.WriteExpression(statement.Expression, formatter);
                    formatter.Write(";");
                }
            }

            private void WriteConditionStatement(IConditionStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("if");
                formatter.Write(" ");
                if (statement.Condition is IBinaryExpression)
                    this.WriteExpression(statement.Condition, formatter);
                else
                {
                    formatter.Write("(");
                    this.WriteExpression(statement.Condition, formatter);
                    formatter.Write(")");
                }

                formatter.Write(" {");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (statement.Then != null)
                    this.WriteStatement(statement.Then, formatter);
                else
                    formatter.WriteLine();

                formatter.WriteOutdent();
                formatter.Write("}");

                if ((statement.Else != null) && (statement.Else.Statements.Count > 0))
                {
                    this.WritePendingOutdent(formatter);
                    formatter.WriteLine();
                    formatter.WriteKeyword("else");
                    formatter.Write(" {");
                    formatter.WriteLine();
                    formatter.WriteIndent();
                    if (statement.Else != null)
                    {
                        this.WriteStatement(statement.Else, formatter);
                        this.WritePendingOutdent(formatter);
                    }
                    else
                    {
                        formatter.WriteLine();
                    }
                    formatter.WriteOutdent();
                    formatter.Write("}");
                }
            }

            private void WriteTryCatchFinallyStatement(ITryCatchFinallyStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("try");
                formatter.Write(" {");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (statement.Try != null)
                {
                    this.WriteStatement(statement.Try, formatter);
                    this.WritePendingOutdent(formatter);
                }
                else
                {
                    formatter.WriteLine();
                }
                formatter.WriteOutdent();
                formatter.Write("}");

                this.firstStmt = true;
                foreach (ICatchClause catchClause in statement.CatchClauses)
                {
                    formatter.WriteLine();
                    formatter.WriteKeyword("catch");
                    formatter.Write(" (");
                    formatter.WriteDeclaration(catchClause.Variable.Name);
                    formatter.Write(")");
                    formatter.Write(" {");
                    formatter.WriteLine();
                    formatter.WriteIndent();

                    if (catchClause.Condition != null)
                    {
                        formatter.Write(" ");
                        formatter.WriteKeyword("if");
                        formatter.Write(" ");
                        this.WriteExpression(catchClause.Condition, formatter);
                        formatter.Write(" ");
                        formatter.WriteKeyword("then");
                    }

                    if (catchClause.Body != null)
                    {
                        this.WriteStatement(catchClause.Body, formatter);
                    }
                    else
                    {
                        formatter.WriteLine();
                    }

                    formatter.WriteOutdent();
                    formatter.Write("}");
                }

                if ((statement.Finally != null) && (statement.Finally.Statements.Count > 0))
                {
                    formatter.WriteLine();
                    formatter.WriteKeyword("finally");
                    formatter.Write(" {");
                    formatter.WriteLine();
                    formatter.WriteIndent();
                    if (statement.Finally != null)
                    {
                        this.WriteStatement(statement.Finally, formatter);
                        this.WritePendingOutdent(formatter);
                    }
                    else
                    {
                        formatter.WriteLine();
                    }
                    formatter.WriteOutdent();
                    formatter.Write("}");
                }
            }

            private void WriteAssignExpression(IAssignExpression value, IFormatter formatter)
            {
                IBinaryExpression binaryExpression = value.Expression as IBinaryExpression;
                if (binaryExpression != null)
                {
                    if (value.Target.Equals(binaryExpression.Left))
                    {
                        string operatorText = string.Empty;
                        switch (binaryExpression.Operator)
                        {
                            case BinaryOperator.Add:
                                operatorText = "inc";
                                break;

                            case BinaryOperator.Subtract:
                                operatorText = "dec";
                                break;
                        }

                        if (operatorText.Length != 0)
                        {
                            // Op(a,b)
                            formatter.Write(operatorText);
                            formatter.Write("(");
                            this.WriteExpression(value.Target, formatter);
                            formatter.Write(",");
                            formatter.Write(" ");
                            this.WriteExpression(binaryExpression.Right, formatter);
                            formatter.Write(")");

                            return;
                        }
                    }
                }

                IPropertyReferenceExpression propExpression = value.Target as IPropertyReferenceExpression;
                if (propExpression != null)
                {
                    if (propExpression.Target != null)
                    {
                        this.WriteTargetExpression(propExpression.Target, formatter);
                        formatter.Write(".");
                    }
                    var s = propExpression.Property.Resolve().SetMethod;
                    WriteMethodReference(s, formatter);
                    formatter.Write("(");
                    this.WriteExpression(value.Expression, formatter);
                    formatter.Write(")");
                }
                else
                {
                    // x := y + z
                    this.WriteExpression(value.Target, formatter);
                    formatter.Write(" = ");
                    this.WriteExpression(value.Expression, formatter);
                }
            }

            private void WriteExpressionStatement(IExpressionStatement statement, IFormatter formatter)
            { // in Delphi we have to filter the IExpressionStatement that is a IVariableDeclarationExpression
                // as this is defined/dumped in the method's var section by WriteVariableList
                if (!(statement.Expression is IVariableDeclarationExpression))
                {
                    this.WriteStatementSeparator(formatter);
                    IUnaryExpression unaryExpression = statement.Expression as IUnaryExpression;
                    if (unaryExpression != null && unaryExpression.Operator == UnaryOperator.PostIncrement)
                    {
                        this.WriteExpression(unaryExpression.Expression, formatter);
                        formatter.Write("++");
                    }
                    else if (unaryExpression != null && unaryExpression.Operator == UnaryOperator.PostDecrement)
                    {
                        this.WriteExpression(unaryExpression.Expression, formatter);
                        formatter.Write("--");
                    }
                    else
                    {
                        this.WriteExpression(statement.Expression, formatter);
                    }
                }
            }

            private void WriteForStatement(IForStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);


                formatter.WriteKeyword("for");
                formatter.Write(" (");
                this.forLoop = true;
                this.WriteStatement(statement.Initializer, formatter);
                formatter.Write("; ");
                this.WriteExpression(statement.Condition, formatter);
                formatter.Write("; ");
                this.WriteStatement(statement.Increment, formatter);
                formatter.Write(")");
                this.forLoop = false;
                formatter.Write(" {");
                formatter.WriteLine();
                formatter.WriteIndent();
                if (statement.Body != null)
                {
                    this.WriteStatement(statement.Body, formatter);
                }
                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.WriteKeyword("}");


            }

            private void WriteForEachStatement(IForEachStatement value, IFormatter formatter)
            {
                // TODO statement.Variable declaration needs to be rendered some where

                this.WriteStatementSeparator(formatter);

                TextFormatter description = new TextFormatter();
                this.WriteVariableDeclaration(value.Variable, description);

                formatter.WriteLine();
                formatter.WriteKeyword("foreach");
                formatter.Write(" (");
                formatter.WriteReference(value.Variable.Name, description.ToString(), null);
                formatter.WriteKeyword(" in ");
                this.WriteExpression(value.Expression, formatter);
                formatter.Write(") {");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (value.Body != null)
                {
                    this.WriteStatement(value.Body, formatter);
                }

                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.WriteKeyword("}");
            }

            private void WriteUsingStatement(IUsingStatement statement, IFormatter formatter)
            {
                IVariableReference variable = null;

                IAssignExpression assignExpression = statement.Expression as IAssignExpression;
                if (assignExpression != null)
                {
                    IVariableDeclarationExpression variableDeclarationExpression = assignExpression.Target as IVariableDeclarationExpression;
                    if (variableDeclarationExpression != null)
                    {
                        variable = variableDeclarationExpression.Variable;
                    }

                    IVariableReferenceExpression variableReferenceExpression = assignExpression.Target as IVariableReferenceExpression;
                    if (variableReferenceExpression != null)
                    {
                        variable = variableReferenceExpression.Variable;
                    }
                }

                this.WriteStatementSeparator(formatter);
                // make a comment that Reflector detected this as a using statement
                //formatter.Write("{using");

                if (variable != null)
                {
                    //formatter.Write(" ");
                    this.WriteVariableReference(variable, formatter);
                }

                formatter.Write("}");
                formatter.WriteLine();

                // and replace this with
                // - create obj
                // - try ... finally obj.Dispose end

                formatter.WriteKeyword("begin");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (variable != null)
                {
                    this.WriteVariableReference(variable, formatter);
                    formatter.Write(" ");
                    formatter.WriteKeyword(":=");
                    formatter.Write(" ");
                    this.WriteExpression(assignExpression.Expression, formatter);
                    this.WriteStatementSeparator(formatter);
                }

                formatter.WriteKeyword("try");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (statement.Body != null)
                {
                    this.WriteBlockStatement(statement.Body, formatter);
                }

                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.WriteKeyword("finally");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (variable != null)
                {
                    this.firstStmt = true;
                    this.WriteVariableReference(variable, formatter);
                    formatter.Write(".");
                    formatter.Write("Dispose");
                    formatter.WriteLine();
                }
                else
                {
                    this.firstStmt = true;
                    this.WriteExpression(statement.Expression);
                    formatter.Write(".");
                    formatter.Write("Dispose");
                    formatter.WriteLine();
                }

                formatter.WriteOutdent();
                formatter.WriteKeyword("end");
                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.WriteKeyword("end");
            }

            private void WriteFixedStatement(IFixedStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("fixed");
                formatter.Write(" ");
                formatter.Write("(");
                this.WriteVariableDeclaration(statement.Variable, formatter);
                formatter.Write(" ");
                formatter.WriteKeyword("=");
                formatter.Write(" ");
                this.WriteExpression(statement.Expression, formatter);
                formatter.Write(")");

                formatter.WriteLine();
                formatter.WriteKeyword("begin");
                formatter.WriteLine();
                formatter.WriteIndent();

                if (statement.Body != null)
                {
                    this.WriteBlockStatement(statement.Body, formatter);
                }

                formatter.WriteOutdent();
                formatter.WriteKeyword("end ");
            }

            private void WriteWhileStatement(IWhileStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("while");
                formatter.Write(" ");
                formatter.Write("(");
                if (statement.Condition != null)
                {

                    this.WriteExpression(statement.Condition, formatter);

                }
                else
                    formatter.WriteLiteral("true");
                formatter.Write(")");

                formatter.Write(" {");
                formatter.WriteLine();
                formatter.WriteIndent();
                if (statement.Body != null)
                {
                    this.WriteStatement(statement.Body, formatter);
                }
                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.Write("}");
            }

            private void WriteDoStatement(IDoStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("do");
                formatter.Write(" {");
                formatter.WriteLine();
                formatter.WriteIndent();
                if (statement.Body != null)
                {
                    this.WriteStatement(statement.Body, formatter);
                }
                formatter.WriteLine();
                formatter.WriteOutdent();
                formatter.Write("} ");
                formatter.WriteKeyword("while");
                formatter.Write(" (");

                if (statement.Condition != null)
                {
                    this.WriteExpression(statement.Condition, formatter);
                }
                else
                {
                    formatter.WriteLiteral("true");
                }
                formatter.Write(" );");
            }

            private void WriteBreakStatement(IBreakStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("break");
                formatter.Write(";");
                formatter.WriteLine();
            }

            private void WriteContinueStatement(IContinueStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("continue");
                formatter.Write(";");
                formatter.WriteLine();
            }

            private void WriteThrowExceptionStatement(IThrowExceptionStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("raise");
                formatter.Write(" ");
                if (statement.Expression != null)
                    this.WriteExpression(statement.Expression, formatter);
                else
                {
                    this.WriteDeclaration("Exception", formatter);
                    formatter.Write(".");
                    formatter.WriteKeyword("Create");
                }
            }

            private void WriteVariableDeclarationExpression(IVariableDeclarationExpression expression, IFormatter formatter)
            { // this.WriteVariableDeclaration(formatter, expression.Variable); // this is for C#
                //
                // no variable declaration expression in Delphi. Convert this to a variable reference only!
                this.WriteVariableReference(expression.Variable, formatter);
            }

            private void WriteVariableDeclaration(IVariableDeclaration variableDeclaration, IFormatter formatter)
            {
                formatter.WriteKeyword("var ");
                this.WriteDeclaration(variableDeclaration.Name, formatter); // TODO Escape = true

                if (!this.forLoop)
                {
                    formatter.Write(";");
                    formatter.WriteLine();
                }
            }

            private void WriteAttachEventStatement(IAttachEventStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                this.WriteEventReferenceExpression(statement.Event, formatter);
                formatter.Write(" += ");
                this.WriteExpression(statement.Listener, formatter);
                formatter.Write(";");
                formatter.WriteLine();
            }

            private void WriteRemoveEventStatement(IRemoveEventStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                this.WriteEventReferenceExpression(statement.Event, formatter);
                formatter.Write(" -= ");
                this.WriteExpression(statement.Listener, formatter);
                formatter.Write(";");
                formatter.WriteLine();
            }

            private void WriteSwitchStatement(ISwitchStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);

                formatter.WriteKeyword("switch");
                formatter.Write(" (");
                this.WriteExpression(statement.Expression, formatter);
                formatter.Write(") ");
                formatter.Write("{");
                formatter.WriteLine();
                foreach (ISwitchCase switchCase in statement.Cases)
                {
                    IConditionCase conditionCase = switchCase as IConditionCase;
                    if (conditionCase != null)
                    {
                        this.WriteSwitchCaseCondition(conditionCase.Condition, formatter);
                    }

                    IDefaultCase defaultCase = switchCase as IDefaultCase;
                    if (defaultCase != null)
                    {
                        formatter.WriteKeyword("default");
                        formatter.Write(":");
                    }

                    formatter.WriteIndent();

                    if (switchCase.Body != null)
                    {
                        this.WriteStatement(switchCase.Body, formatter);
                        this.WritePendingOutdent(formatter);
                    }
                    else
                    {
                        formatter.WriteLine();
                    }

                    formatter.WriteOutdent();

                }
                formatter.WriteKeyword("}");
            }

            private void WriteSwitchCaseCondition(IExpression condition, IFormatter formatter)
            {
                IBinaryExpression binaryExpression = condition as IBinaryExpression;
                if ((binaryExpression != null) && (binaryExpression.Operator == BinaryOperator.BooleanOr))
                {
                    this.WriteSwitchCaseCondition(binaryExpression.Left, formatter);
                    this.WriteSwitchCaseCondition(binaryExpression.Right, formatter);
                }
                else
                {
                    formatter.WriteKeyword("case ");
                    this.WriteExpression(condition, formatter);
                    formatter.Write(":");
                    formatter.WriteLine();
                }
            }

            private void WriteGotoStatement(IGotoStatement statement, IFormatter formatter)
            {
                this.WriteStatementSeparator(formatter);
                formatter.WriteKeyword("goto");
                formatter.Write(" ");
                this.WriteDeclaration(statement.Name, formatter);
            }

            private void WriteLabeledStatement(ILabeledStatement statement, IFormatter formatter)
            {
                if (statement.Statement != null)
                {
                    this.WriteStatementSeparator(formatter);
                }
                formatter.WriteLine();
                formatter.WriteOutdent();
                this.WriteDeclaration(statement.Name, formatter);
                formatter.Write(":");
                formatter.WriteLine();
                formatter.WriteIndent();
                this.firstStmt = true;
                if (statement.Statement != null)
                {
                    this.WriteStatement(statement.Statement, formatter);
                }
            }
            #endregion

            private void WriteDeclaringType(ITypeReference value, IFormatter formatter)
            {
                formatter.WriteProperty("Declaring Type", GetDelphiStyleResolutionScope(value));
                this.WriteDeclaringAssembly(Helper.GetAssemblyReference(value), formatter);
            }

            private void WriteDeclaringAssembly(IAssemblyReference value, IFormatter formatter)
            {
                if (value != null)
                {
                    string text = ((value.Name != null) && (value.Version != null)) ? (value.Name + ", Version=" + value.Version.ToString()) : value.ToString();
                    formatter.WriteProperty("Assembly", text);
                }
            }

            private string GetTypeReferenceDescription(ITypeReference typeReference)
            {
                return Helper.GetNameWithResolutionScope(typeReference);
            }

            private string GetFieldReferenceDescription(IFieldReference fieldReference)
            {
                IFormatter formatter = new TextFormatter();

                this.WriteType(fieldReference.FieldType, formatter);
                formatter.Write(" ");
                formatter.Write(this.GetTypeReferenceDescription(fieldReference.DeclaringType as ITypeReference));
                formatter.Write(".");
                this.WriteDeclaration(fieldReference.Name, formatter);
                formatter.Write(";");

                return formatter.ToString();
            }

            private string GetMethodReferenceDescription(IMethodReference value)
            {
                IFormatter formatter = new TextFormatter();

                if (this.IsConstructor(value))
                {
                    formatter.Write(this.GetTypeReferenceDescription(value.DeclaringType as ITypeReference));
                    formatter.Write(".");
                    formatter.Write(Helper.GetNameWithResolutionScope(value.DeclaringType as ITypeReference));
                }
                else
                {
                    // TODO custom attributes [return: ...]
                    this.WriteType(value.ReturnType.Type, formatter);
                    formatter.Write(" ");
                    formatter.Write(Helper.GetNameWithResolutionScope(value.DeclaringType as ITypeReference));
                    formatter.Write(".");
                    formatter.Write(value.Name);
                }

                this.WriteGenericArgumentList(value.GenericArguments, formatter);

                formatter.Write("(");

                this.WriteParameterDeclarationList(value.Parameters, formatter, null);

                if (value.CallingConvention == MethodCallingConvention.VariableArguments)
                {
                    formatter.WriteKeyword(", __arglist");
                }

                formatter.Write(")");
                formatter.Write(";");

                return formatter.ToString();
            }

            private string GetPropertyReferenceDescription(IPropertyReference propertyReference)
            {
                IFormatter formatter = new TextFormatter();

                this.WriteType(propertyReference.PropertyType, formatter);
                formatter.Write(" ");

                // Name
                string propertyName = propertyReference.Name;
                if (propertyName == "Item")
                {
                    propertyName = "this";
                }

                formatter.Write(this.GetTypeReferenceDescription(propertyReference.DeclaringType as ITypeReference));
                formatter.Write(".");
                this.WriteDeclaration(propertyName, formatter);

                // Parameters
                IParameterDeclarationCollection parameters = propertyReference.Parameters;
                if (parameters.Count > 0)
                {
                    formatter.Write("(");
                    this.WriteParameterDeclarationList(parameters, formatter, null);
                    formatter.Write(")");
                }

                formatter.Write(" ");
                formatter.Write("{ ... }");

                return formatter.ToString();
            }

            private string GetEventReferenceDescription(IEventReference eventReference)
            {
                IFormatter formatter = new TextFormatter();

                formatter.WriteKeyword("event");
                formatter.Write(" ");
                this.WriteType(eventReference.EventType, formatter);
                formatter.Write(" ");
                formatter.Write(this.GetTypeReferenceDescription(eventReference.DeclaringType as ITypeReference));
                formatter.Write(".");
                this.WriteDeclaration(eventReference.Name, formatter);
                formatter.Write(";");

                return formatter.ToString();
            }

            private static bool IsType(IType value, string namespaceName, string name)
            {
                return (IsType(value, namespaceName, name, "mscorlib") || IsType(value, namespaceName, name, "sscorlib"));
            }

            private static bool IsType(IType value, string namespaceName, string name, string assemblyName)
            {
                ITypeReference typeReference = value as ITypeReference;
                if (typeReference != null)
                {
                    return ((typeReference.Name == name) && (typeReference.Namespace == namespaceName) && (IsAssemblyReference(typeReference, assemblyName)));
                }

                IRequiredModifier requiredModifier = value as IRequiredModifier;
                if (requiredModifier != null)
                {
                    return IsType(requiredModifier.ElementType, namespaceName, name);
                }

                IOptionalModifier optionalModifier = value as IOptionalModifier;
                if (optionalModifier != null)
                {
                    return IsType(optionalModifier.ElementType, namespaceName, name);
                }

                return false;
            }

            private static bool IsAssemblyReference(ITypeReference value, string assemblyName)
            {
                return (Helper.GetAssemblyReference(value).Name == assemblyName);
            }

            private ICustomAttribute GetCustomAttribute(ICustomAttributeProvider value, string namespaceName, string name)
            {
                ICustomAttribute customAttribute = this.GetCustomAttribute(value, namespaceName, name, "mscorlib");

                if (customAttribute == null)
                {
                    customAttribute = this.GetCustomAttribute(value, namespaceName, name, "sscorlib");
                }

                return customAttribute;
            }

            private ICustomAttribute GetCustomAttribute(ICustomAttributeProvider value, string namespaceName, string name, string assemblyName)
            {
                foreach (ICustomAttribute customAttribute in value.Attributes)
                {
                    if (IsType(customAttribute.Constructor.DeclaringType, namespaceName, name, assemblyName))
                    {
                        return customAttribute;
                    }
                }

                return null;
            }

            private ILiteralExpression GetDefaultParameterValue(IParameterDeclaration value)
            {
                ICustomAttribute customAttribute = this.GetCustomAttribute(value, "System.Runtime.InteropServices", "DefaultParameterValueAttribute", "System");
                if ((customAttribute != null) && (customAttribute.Arguments.Count == 1))
                {
                    return customAttribute.Arguments[0] as ILiteralExpression;
                }

                return null;
            }

            private bool IsConstructor(IMethodReference value)
            {
                return ((value.Name == ".ctor") || (value.Name == ".cctor"));
            }

            private bool IsEnumerationElement(IFieldDeclaration value)
            {
                IType fieldType = value.FieldType;
                IType declaringType = value.DeclaringType;
                if (fieldType.Equals(declaringType))
                {
                    ITypeReference typeReference = fieldType as ITypeReference;
                    if (typeReference != null)
                    {
                        return Helper.IsEnumeration(typeReference);
                    }
                }

                return false;
            }

            private string QuoteLiteralExpression(string text)
            {
                using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        char character = text[i];
                        ushort value = (ushort)character;
                        if (value > 0x00ff)
                        {
                            writer.Write("\\u" + value.ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            switch (character)
                            {
                                case '\r': writer.Write("\\r"); break;
                                case '\t': writer.Write("\\t"); break;
                                case '\'': writer.Write("\\\'"); break;
                                case '\0': writer.Write("\\0"); break;
                                case '\n': writer.Write("\\n"); break;
                                default: writer.Write(character); break;
                            }
                        }
                    }
                    return writer.ToString();
                }
            }

            private void WriteDeclaration(string name, IFormatter formatter)
            {
                formatter.WriteDeclaration((Array.IndexOf(this.keywords, name) != -1) ? ("@" + name) : name);
            }

            private void WriteDeclaration(string name, object target, IFormatter formatter)
            {
                formatter.WriteDeclaration((Array.IndexOf(this.keywords, name) != -1) ? ("&" + name) : name, target);
            }

            private void WriteReference(string name, IFormatter formatter, string toolTip, object reference)
            {
                string text = name;
                if (name.Equals(".ctor"))
                {
                    text = "Create";
                }
                if (name.Equals("..ctor"))
                {
                    text = "Create";
                }
                if (Array.IndexOf(this.keywords, name) != -1)
                {
                    text = "&" + name;
                }
                formatter.WriteReference(text, toolTip, reference);
            }

            private string[] keywords = new string[] {
					"and",            "array",         "as",           "asm",
					"begin",          "case",          "class",        "const",
					"constructor",    "destructor",    "dispinterface","div",
					"do",             "downto",        "else",         "end",
					"except",         "exports",       "file",         "finalization",
					"finally",        "for",           "function",     "goto",
					"if",             "implementation","in",           "inherited",
					"initialization", "inline",        "interface",    "is",
					"label",          "library",       "mod",          "nil",
					"not",            "object",        "of",           "or",
					"out",            "packed",        "procedure",    "program",
					"property",       "raise",         "record",       "repeat",
					"resourcestring", "set",           "shl",          "shr",
					/*"string", */    "then",          "threadvar",    "to",
					"try",            "type",          "unit",         "until",
					"uses",           "var",           "while",        "with",
					"xor"
				};

            private class TextFormatter : IFormatter
            {
                private StringWriter writer = new StringWriter(CultureInfo.InvariantCulture);
                private bool newLine;
                private int indent = 0;

                public override string ToString()
                {
                    return this.writer.ToString();
                }

                public void Write(string text)
                {
                    this.ApplyIndent();
                    this.writer.Write(text);
                }

                public void WriteDeclaration(string text)
                {
                    this.WriteBold(text);
                }

                public void WriteDeclaration(string text, object target)
                {
                    this.WriteBold(text);
                }

                public void WriteComment(string text)
                {
                    this.WriteColor(text, (int)0x808080);
                }

                public void WriteLiteral(string text)
                {
                    this.WriteColor(text, (int)0x800000);
                }

                public void WriteKeyword(string text)
                {
                    this.WriteColor(text, (int)0x000080);
                }

                public void WriteIndent()
                {
                    this.indent++;
                }

                public void WriteLine()
                {
                    this.writer.WriteLine();
                    this.newLine = true;
                }

                public void WriteOutdent()
                {
                    this.indent--;
                }

                public void WriteReference(string text, string toolTip, Object reference)
                {
                    this.ApplyIndent();
                    this.writer.Write(text);
                }

                public void WriteProperty(string propertyName, string propertyValue)
                {
                }

                private void WriteBold(string text)
                {
                    this.ApplyIndent();
                    this.writer.Write(text);
                }

                private void WriteColor(string text, int color)
                {
                    this.ApplyIndent();
                    this.writer.Write(text);
                }

                private void ApplyIndent()
                {
                    if (this.newLine)
                    {
                        for (int i = 0; i < this.indent; i++)
                        {
                            this.writer.Write("    ");
                        }

                        this.newLine = false;
                    }
                }
            }
        }
    }
}
