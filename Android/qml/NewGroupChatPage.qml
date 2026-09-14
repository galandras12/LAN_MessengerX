import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page

    //	Set (with excludeIds) when navigated to from an already-open
    //	GroupChatPage's "invite more" button rather than from the "New
    //	group chat" entry point - switches this page from creating a new
    //	room to inviting more people into threadId.
    property string existingThreadId: ""
    property var excludeIds: []
    property var selectedIds: []

    title: existingThreadId.length > 0 ? qsTr("Add People") : qsTr("New Group Chat")

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 4

            ToolButton {
                text: "←"
                onClicked: StackView.view.pop()
            }
            Label {
                text: page.title
                font.pixelSize: 18
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
            ToolButton {
                text: page.existingThreadId.length > 0 ? qsTr("Add") : qsTr("Create")
                enabled: page.selectedIds.length > 0
                onClicked: {
                    if(page.existingThreadId.length > 0) {
                        messenger.addParticipantsToRoom(page.existingThreadId, page.selectedIds)
                        StackView.view.pop()
                    } else {
                        var threadId = messenger.createGroupChat(page.selectedIds)
                        StackView.view.pop()
                        StackView.view.push(Qt.resolvedUrl("GroupChatPage.qml"), {"threadId": threadId})
                    }
                }
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Label {
            text: qsTr("Select people to invite")
            opacity: 0.6
            font.pixelSize: 12
            Layout.fillWidth: true
            Layout.margins: 12
        }

        ListView {
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: messenger.contacts

            delegate: CheckDelegate {
                readonly property bool alreadyIn: page.excludeIds.indexOf(userId) !== -1

                width: ListView.view.width
                text: alreadyIn ? qsTr("%1 (already in this chat)").arg(name) : name
                enabled: !alreadyIn
                checked: alreadyIn

                onCheckedChanged: {
                    if(alreadyIn)
                        return
                    var ids = page.selectedIds.slice()
                    var idx = ids.indexOf(userId)
                    if(checked && idx === -1)
                        ids.push(userId)
                    else if(!checked && idx !== -1)
                        ids.splice(idx, 1)
                    page.selectedIds = ids
                }
            }
        }
    }
}
