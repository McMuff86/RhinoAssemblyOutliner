// cmdNativeTest.cpp : command file
//

#include "stdafx.h"
#include "RhinoAssemblyOutliner.nativePlugIn.h"

////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////
//
// BEGIN NativeTest command
//

#pragma region NativeTest command

// Do NOT put the definition of class CCommandNativeTest in a header
// file. There is only ONE instance of a CCommandNativeTest class
// and that instance is the static theNativeTestCommand that appears
// immediately below the class definition.

class CCommandNativeTest : public CRhinoCommand
{
public:
  // The one and only instance of CCommandNativeTest is created below.
  // No copy constructor or operator= is required.
  // Values of member variables persist for the duration of the application.

  // CCommandNativeTest::CCommandNativeTest()
  // is called exactly once when static theNativeTestCommand is created.
  CCommandNativeTest() = default;

  // CCommandNativeTest::~CCommandNativeTest()
  // is called exactly once when static theNativeTestCommand is destroyed.
  // The destructor should not make any calls to the Rhino SDK. 
  // If your command has persistent settings, then override 
  // CRhinoCommand::SaveProfile and CRhinoCommand::LoadProfile.
  ~CCommandNativeTest() = default;

  // Returns a unique UUID for this command.
  // If you try to use an id that is already being used, then
  // your command will not work. Use GUIDGEN.EXE to make unique UUID.
  UUID CommandUUID() override
  {
    // {B19D8DB0-C0A0-4765-9072-45FBB0AB2EC9}
    static const GUID NativeTestCommand_UUID = 
    {0xb19d8db0,0xc0a0,0x4765,{0x90,0x72,0x45,0xfb,0xb0,0xab,0x2e,0xc9}};
    return NativeTestCommand_UUID;
  }

  // Returns the English command name.
  // If you want to provide a localized command name, then override 
  // CRhinoCommand::LocalCommandName.
  const wchar_t* EnglishCommandName() override { return L"NativeTest"; }

  // Rhino calls RunCommand to run the command.
  CRhinoCommand::result RunCommand(const CRhinoCommandContext& context) override;
};

// The one and only CCommandNativeTest object
// Do NOT create any other instance of a CCommandNativeTest class.
static class CCommandNativeTest theNativeTestCommand;

CRhinoCommand::result CCommandNativeTest::RunCommand(const CRhinoCommandContext& context)
{
  // CCommandNativeTest::RunCommand() is called when the user
  // runs the "NativeTest".

  // TODO: Add command code here.

  // Rhino command that display a dialog box interface should also support
  // a command-line, or script-able interface.

  ON_wString str;
  str.Format(L"The \"%s\" command is under construction.\n", EnglishCommandName());
  const wchar_t* pszStr = static_cast<const wchar_t*>(str);
  if (context.IsInteractive())
    RhinoMessageBox(pszStr, RhinoAssemblyOutliner_nativePlugIn().PlugInName(), MB_OK);
  else
    RhinoApp().Print(pszStr);

  // TODO: Return one of the following values:
  //   CRhinoCommand::success:  The command worked.
  //   CRhinoCommand::failure:  The command failed because of invalid input, inability
  //                            to compute the desired result, or some other reason
  //                            computation reason.
  //   CRhinoCommand::cancel:   The user interactively canceled the command 
  //                            (by pressing ESCAPE, clicking a CANCEL button, etc.)
  //                            in a Get operation, dialog, time consuming computation, etc.

  return CRhinoCommand::success;
}

#pragma endregion

//
// END NativeTest command
//
////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////
