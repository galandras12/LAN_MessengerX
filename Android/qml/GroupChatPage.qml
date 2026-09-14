import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page

    property string threadId: ""
    property string roomTitleText: threadId.length > 0 ? messenger.roomTitle(threadId) : ""
    property int participantCount: threadId.length > 0 ? (messenger.roomParticipants(threadId) ? messenger.roomParticipants(threadId).rowCount() : 0) : 0
    property ChatModel chatModel: threadId.length > 0 ? messenger.roomMessages(threadId) : null

    title: roomTitleText

    //	roomTitle()/roomParticipants().rowCount() are plain calls, not
    //	NOTIFYing properties - refresh the two properties above explicitly
    //	on MessengerBridge's roomUpdated signal rather than relying on
    //	implicit QML binding reactivity that would not actually trigger
    //	when a room's participant list changes.
    Connections {
        target: messenger
        function onRoomUpdated(id) {
            if(id !== page.threadId)
                return
            page.roomTitleText = messenger.roomTitle(page.threadId)
            var model = messenger.roomParticipants(page.threadId)
            page.participantCount = model ? model.rowCount() : 0
        }
    }

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 4

            ToolButton {
                text: "←"
                onClicked: StackView.view.pop()
            }
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 0
                Label {
                    text: page.roomTitleText
                    font.pixelSize: 16
                    elide: Text.ElideRight
                    Layout.fillWidth: true
                }
                Label {
                    text: qsTr("%1 participants").arg(page.participantCount)
                    font.pixelSize: 11
                    opacity: 0.8
                }
            }
            ToolButton {
                text: qsTr("Leave")
                onClicked: {
                    messenger.leaveGroupChat(page.threadId)
                    StackView.view.pop()
                }
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        ListView {
            id: messageList
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: page.chatModel
            spacing: 8

            onCountChanged: positionViewAtEnd()

            delegate: Item {
                width: ListView.view.width
                height: (isSystem ? sysLabel.implicitHeight : bubble.height) + 8

                Label {
                    id: sysLabel
                    visible: isSystem
                    anchors.horizontalCenter: parent.horizontalCenter
                    text: model.text
                    font.pixelSize: 12
                    opacity: 0.6
                }

                Rectangle {
                    id: bubble
                    visible: !isSystem
                    anchors.right: outgoing ? parent.right : undefined
                    anchors.left: outgoing ? undefined : parent.left
                    anchors.margins: 12
                    width: Math.min(label.implicitWidth + 24, page.width * 0.75)
                    height: bubbleColumn.implicitHeight + 16
                    radius: 14
                    color: outgoing ? Material.accentColor : Material.dividerColor

                    ColumnLayout {
                        id: bubbleColumn
                        anchors.fill: parent
                        anchors.margins: 8
                        spacing: 2

                        Label {
                            visible: !outgoing
                            text: senderName
                            font.bold: true
                            font.pixelSize: 11
                            color: outgoing ? "white" : Material.accentColor
                            Layout.fillWidth: true
                        }
                        Text {
                            id: label
                            text: model.text
                            wrapMode: Text.Wrap
                            color: outgoing ? "white" : Material.foreground
                            Layout.fillWidth: true
                        }
                    }
                }
            }
        }

        RowLayout {
            Layout.fillWidth: true
            Layout.margins: 8

            TextField {
                id: input
                Layout.fillWidth: true
                placeholderText: qsTr("Message…")
                onAccepted: sendButton.clicked()
            }

            Button {
                id: sendButton
                text: qsTr("Send")
                enabled: input.text.length > 0
                onClicked: {
                    messenger.sendGroupMessage(page.threadId, input.text)
                    input.text = ""
                }
            }
        }
    }
}
